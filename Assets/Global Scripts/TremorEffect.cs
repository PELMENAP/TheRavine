using UnityEngine;
using LitMotion;
using LitMotion.Extensions;

public class TremorEffect : MonoBehaviour
{
    [SerializeField] private AnimationCurve tremorCurve;
    [SerializeField] private float duration = 1f;
    void Start()
    {
        LMotion.Create(Vector2.zero, Vector2.one, duration)
            .WithEase(tremorCurve)
            .WithLoops(-1, LoopType.Restart)
            .BindToLocalScaleXY(this.transform);
            // .AddTo(gameObject);
    }
}


//         [SerializeField] ButtonEventTrigger buttonTrigger;

//         [Header("Hover")]
//         [SerializeField] HoverEventTrigger hoverTrigger;
//         [SerializeField] Image fillImage;

//         void Start()
//         {
//             var buttonTransform = (RectTransform)buttonTrigger.transform;
//             var buttonSize = buttonTransform.sizeDelta;
//             buttonTrigger.onPointerDown.AddListener(_ =>
//             {
//                 LMotion.Create(buttonSize, buttonSize - new Vector2(10f, 10f), 0.08f)
//                     .BindToSizeDelta(buttonTransform);
//             });
//             buttonTrigger.onPointerUp.AddListener(_ =>
//             {
//                 LMotion.Create(buttonSize - new Vector2(10f, 10f), buttonSize, 0.08f)
//                     .BindToSizeDelta(buttonTransform);
//             });

//             hoverTrigger.onPointerEnter.AddListener(_ =>
//             {
//                 fillImage.fillOrigin = 0;
//                 LMotion.Create(0f, 1f, 0.1f)
//                     .BindToFillAmount(fillImage);
//             });
//             hoverTrigger.onPointerExit.AddListener(_ =>
//             {
//                 fillImage.fillOrigin = 1;
//                 LMotion.Create(1f, 0f, 0.1f)
//                     .BindToFillAmount(fillImage);
//             });
//         }



            // // Position
            // LMotion.Create(-5f, 5f, 3f)
            //     .WithEase(Ease.InOutSine)
            //     .BindToPositionX(target1);

            // // Position + Rotation
            // LMotion.Create(-5f, 5f, 3f)
            //     .WithEase(Ease.InOutSine)
            //     .BindToPositionX(target2);
            // LMotion.Create(0f, 180f, 3f)
            //     .WithEase(Ease.InOutSine)
            //     .BindToEulerAnglesZ(target2);

            // // Position + Rotation + Scale
            // LMotion.Create(-5f, 5f, 3f)
            //     .WithEase(Ease.InOutSine)
            //     .BindToPositionX(target3);
            // LMotion.Create(0f, 180f, 3f)
            //     .WithEase(Ease.InOutSine)
            //     .BindToEulerAnglesZ(target3);
            // LMotion.Create(new Vector3(1f, 1f, 1f), new Vector3(1.5f, 1.5f, 1.5f), 3f)
            //     .WithEase(Ease.InOutSine)
            //     .BindToLocalScale(target3);



            // for (int i = 0; i < targets.Length; i++)
            // {
            //     LMotion.Create(-5f, 5f, 2f)
            //         .WithEase(Ease.InOutSine)
            //         .WithDelay(i * 0.2f)
            //         .BindToPositionX(targets[i]);
            // }