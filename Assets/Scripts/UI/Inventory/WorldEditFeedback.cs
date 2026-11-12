using System;
using LitMotion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TheRavine.Generator;

namespace TheRavine.Inventory
{
    public sealed class WorldEditFeedback : IDisposable
    {
        private const int MarkerCount = 4;
        private const int FlyerCount = 6;
        private const int RingSize = 64;
        private const float MarkerSize = 72f;
        private const float FlyerSize = 48f;
        private const float MarkerDuration = 0.7f;
        private const float FlightDuration = 0.45f;
        private const float FlightArc = 60f;

        public const string PickUpRefusal = "Can't pick up";

        private static readonly Color RefusalColor = new(1f, 0.32f, 0.28f, 1f);

        private sealed class Marker
        {
            public RectTransform Root;
            public CanvasGroup Group;
            public TextMeshProUGUI Label;
            public MotionHandle Handle;
            public Action Done;

            public void Apply(float t)
            {
                Root.localScale = Vector3.one * Mathf.LerpUnclamped(0.6f, 1.15f, t);
                Group.alpha = 1f - t * t;
            }

            public void Hide()
            {
                Handle = default;
                Root.gameObject.SetActive(false);
            }
        }

        private sealed class Flyer
        {
            public RectTransform Root;
            public Image Image;
            public MotionHandle Handle;
            public Vector2 From;
            public Vector2 To;
            public Action Done;

            public void Apply(float t)
            {
                Vector2 position = Vector2.Lerp(From, To, t);
                position.y += Mathf.Sin(t * Mathf.PI) * FlightArc;
                Root.position = new Vector3(position.x, position.y, 0f);
                Root.localScale = Vector3.one * Mathf.Lerp(1f, 0.6f, t);
            }

            public void Hide()
            {
                Handle = default;
                Image.sprite = null;
                Root.gameObject.SetActive(false);
            }
        }

        private readonly MapGenerator generator;
        private readonly GameObject root;
        private readonly Texture2D ringTexture;
        private readonly Sprite ringSprite;
        private readonly Marker[] markers = new Marker[MarkerCount];
        private readonly Flyer[] flyers = new Flyer[FlyerCount];
        private int markerCursor;
        private int flyerCursor;

        public WorldEditFeedback(MapGenerator generator)
        {
            this.generator = generator;

            root = new GameObject("WorldEditFeedback");
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;

            ringTexture = BuildRingTexture();
            ringSprite = Sprite.Create(ringTexture, new Rect(0f, 0f, RingSize, RingSize), new Vector2(0.5f, 0.5f), RingSize);

            for (int i = 0; i < MarkerCount; i++) markers[i] = BuildMarker();
            for (int i = 0; i < FlyerCount; i++) flyers[i] = BuildFlyer();
        }

        public static string ReasonOf(byte edit, byte placement)
        {
            switch ((WorldEditResult)edit)
            {
                case WorldEditResult.NoObject: return "Nothing here";
                case WorldEditResult.NotPickable: return "Can't pick up";
                case WorldEditResult.UnknownObject: return "Unknown object";
                case WorldEditResult.NotPlaceable: return "Can't place this";
                case WorldEditResult.NotOwned: return "Not yours";
                case WorldEditResult.Rejected: return PlacementReason((PlacementResult)placement);
                default: return "Not allowed";
            }
        }

        public static string PlacementReason(PlacementResult placement)
        {
            switch (placement)
            {
                case PlacementResult.NoChunk: return "Area not loaded";
                case PlacementResult.OutOfBounds: return "Out of bounds";
                case PlacementResult.Occupied: return "Occupied";
                case PlacementResult.UpperLevelOccupied: return "Blocked above";
                case PlacementResult.CrossChunk: return "Crosses chunk edge";
                case PlacementResult.TooManyCells: return "Too large";
                case PlacementResult.InvalidLevels: return "Wrong height";
                default: return "Not allowed";
            }
        }

        public void Refused(long cell, string reason)
        {
            UISoundSystem.Instance?.PlayDeny();
            if (!TryProject(cell, out Vector2 screen)) return;

            Marker marker = markers[markerCursor];
            markerCursor = (markerCursor + 1) % MarkerCount;
            marker.Handle.TryCancel();

            marker.Label.text = reason;
            marker.Root.position = new Vector3(screen.x, screen.y, 0f);
            marker.Root.gameObject.SetActive(true);
            marker.Handle = LMotion.Create(0f, 1f, MarkerDuration)
                .WithEase(Ease.OutQuad)
                .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .WithImmediateBind()
                .WithOnComplete(marker.Done)
                .Bind(marker, static (t, m) => m.Apply(t));
        }

        public void FlyIcon(long cell, Sprite icon, RectTransform target)
        {
            if (icon == null || target == null || !TryProject(cell, out Vector2 from)) return;

            Canvas canvas = target.GetComponentInParent<Canvas>();
            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            Vector2 to = RectTransformUtility.WorldToScreenPoint(cam, target.position);

            Flyer flyer = flyers[flyerCursor];
            flyerCursor = (flyerCursor + 1) % FlyerCount;
            flyer.Handle.TryCancel();

            flyer.Image.sprite = icon;
            flyer.From = from;
            flyer.To = to;
            flyer.Root.gameObject.SetActive(true);
            flyer.Handle = LMotion.Create(0f, 1f, FlightDuration)
                .WithEase(Ease.InOutQuad)
                .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .WithImmediateBind()
                .WithOnComplete(flyer.Done)
                .Bind(flyer, static (t, f) => f.Apply(t));
        }

        private bool TryProject(long cell, out Vector2 screen)
        {
            screen = default;
            Camera cam = Camera.main;
            if (cam == null || generator == null) return false;

            Vector3 world = generator.GetRealPosition(cell);
            world.x += MapGenerator.scale * 0.5f;
            world.z += MapGenerator.scale * 0.5f;
            world.y += 0.5f;

            Vector3 point = cam.WorldToScreenPoint(CurvedWorld.Bend(world));
            if (point.z <= 0f) return false;

            screen = new Vector2(point.x, point.y);
            return true;
        }

        private Marker BuildMarker()
        {
            var marker = new Marker();
            marker.Root = NewRect("Marker", MarkerSize);
            marker.Group = marker.Root.gameObject.AddComponent<CanvasGroup>();
            marker.Group.blocksRaycasts = false;
            marker.Group.interactable = false;

            var ring = marker.Root.gameObject.AddComponent<Image>();
            ring.sprite = ringSprite;
            ring.color = RefusalColor;
            ring.raycastTarget = false;

            var labelObject = new GameObject("Reason", typeof(RectTransform));
            var labelRect = (RectTransform)labelObject.transform;
            labelRect.SetParent(marker.Root, false);
            labelRect.anchorMin = labelRect.anchorMax = new Vector2(0.5f, 0f);
            labelRect.pivot = new Vector2(0.5f, 1f);
            labelRect.anchoredPosition = new Vector2(0f, -4f);
            labelRect.sizeDelta = new Vector2(220f, 28f);

            marker.Label = labelObject.AddComponent<TextMeshProUGUI>();
            marker.Label.alignment = TextAlignmentOptions.Top;
            marker.Label.fontSize = 22f;
            marker.Label.color = RefusalColor;
            marker.Label.raycastTarget = false;

            marker.Done = marker.Hide;
            marker.Root.gameObject.SetActive(false);
            return marker;
        }

        private Flyer BuildFlyer()
        {
            var flyer = new Flyer();
            flyer.Root = NewRect("Flyer", FlyerSize);
            flyer.Image = flyer.Root.gameObject.AddComponent<Image>();
            flyer.Image.raycastTarget = false;
            flyer.Image.preserveAspect = true;
            flyer.Done = flyer.Hide;
            flyer.Root.gameObject.SetActive(false);
            return flyer;
        }

        private RectTransform NewRect(string name, float size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(root.transform, false);
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);
            return rect;
        }

        private static Texture2D BuildRingTexture()
        {
            var texture = new Texture2D(RingSize, RingSize, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            float center = (RingSize - 1) * 0.5f;
            float outer = RingSize * 0.5f - 1f;
            float inner = outer - 5f;
            var pixels = new Color32[RingSize * RingSize];
            for (int y = 0; y < RingSize; y++)
            for (int x = 0; x < RingSize; x++)
            {
                float d = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center));
                float edge = Mathf.Clamp01(Mathf.Min(outer - d, d - inner));
                pixels[y * RingSize + x] = new Color32(255, 255, 255, (byte)(edge * 255f));
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        public void Dispose()
        {
            for (int i = 0; i < MarkerCount; i++) markers[i].Handle.TryCancel();
            for (int i = 0; i < FlyerCount; i++) flyers[i].Handle.TryCancel();
            if (root != null) UnityEngine.Object.Destroy(root);
            if (ringSprite != null) UnityEngine.Object.Destroy(ringSprite);
            if (ringTexture != null) UnityEngine.Object.Destroy(ringTexture);
        }
    }
}
