using R3;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Linq;

namespace TheRavine.Base
{
    public static class UIBindingHelpers
    {
        public static IDisposable BindToText(
            this Observable<string> source,
            TextMeshProUGUI text)
        {
            return source.Subscribe(value => 
            {
                if (text != null) text.text = value;
            });
        }

        public static IDisposable BindToToggle(
            this Observable<bool> source,
            Toggle toggle)
        {
            return source.Subscribe(value => 
            {
                if (toggle != null) toggle.SetIsOnWithoutNotify(value);
            });
        }

        public static IDisposable BindToDropdown(
            this Observable<int> source,
            TMP_Dropdown dropdown)
        {
            return source.Subscribe(value => 
            {
                if (dropdown != null && value >= 0 && value < dropdown.options.Count)
                    dropdown.SetValueWithoutNotify(value);
            });
        }

        public static IDisposable BindToInputField(
            this Observable<float> source,
            TMP_InputField input,
            string format = "F2")
        {
            return source.Subscribe(value => 
            {
                if (input != null) 
                    input.SetTextWithoutNotify(value.ToString(format));
            });
        }

        public static IDisposable BindToInputField(
            this Observable<int> source,
            TMP_InputField input)
        {
            return source.Subscribe(value => 
            {
                if (input != null) 
                    input.SetTextWithoutNotify(value.ToString());
            });
        }

        public static IDisposable BindToSlider(
            this Observable<float> source,
            Slider slider)
        {
            return source.Subscribe(value => 
            {
                if (slider != null) 
                    slider.SetValueWithoutNotify(value);
            });
        }

        public static IDisposable BindToGameObjectActive(
            this Observable<bool> source,
            GameObject gameObject)
        {
            return source.Subscribe(active => 
            {
                if (gameObject != null) 
                    gameObject.SetActive(active);
            });
        }

        public static IDisposable BindToInteractable(
            this Observable<bool> source,
            Selectable selectable)
        {
            return source.Subscribe(interactable => 
            {
                if (selectable != null) 
                    selectable.interactable = interactable;
            });
        }

        public static IDisposable BindToColor(
            this Observable<Color> source,
            Graphic graphic)
        {
            return source.Subscribe(color => 
            {
                if (graphic != null) 
                    graphic.color = color;
            });
        }

        public static IDisposable BindToAction(
            this Toggle toggle,
            Action<bool> action)
        {
            toggle.onValueChanged.AddListener(value => action?.Invoke(value));
            return Disposable.Create(() => toggle.onValueChanged.RemoveAllListeners());
        }

        public static IDisposable BindToAction(
            this TMP_Dropdown dropdown,
            Action<int> action)
        {
            dropdown.onValueChanged.AddListener(value => action?.Invoke(value));
            return Disposable.Create(() => dropdown.onValueChanged.RemoveAllListeners());
        }

        public static IDisposable BindToAction(
            this Button button,
            Action action)
        {
            button.onClick.AddListener(() => action?.Invoke());
            return Disposable.Create(() => button.onClick.RemoveAllListeners());
        }

        public static IDisposable BindToFloatAction(
            this TMP_InputField input,
            Action<float> action,
            float min = float.MinValue,
            float max = float.MaxValue)
        {
            input.onValueChanged.AddListener(text =>
            {
                if (float.TryParse(text, out float value))
                {
                    value = Mathf.Clamp(value, min, max);
                    action?.Invoke(value);
                }
            });
            return Disposable.Create(() => input.onValueChanged.RemoveAllListeners());
        }

        public static IDisposable BindToIntAction(
            this TMP_InputField input,
            Action<int> action,
            int min = int.MinValue,
            int max = int.MaxValue)
        {
            input.onValueChanged.AddListener(text =>
            {
                if (int.TryParse(text, out int value))
                {
                    value = Mathf.Clamp(value, min, max);
                    action?.Invoke(value);
                }
            });
            return Disposable.Create(() => input.onValueChanged.RemoveAllListeners());
        }

        public static IDisposable BindToEnumAction<TEnum>(
            this TMP_Dropdown dropdown,
            Action<TEnum> action) where TEnum : Enum
        {
            dropdown.onValueChanged.AddListener(index =>
            {
                var enumValue = (TEnum)Enum.ToObject(typeof(TEnum), index);
                action?.Invoke(enumValue);
            });
            return Disposable.Create(() => dropdown.onValueChanged.RemoveAllListeners());
        }
    }

    public static class UISetupHelpers
    {
        public static void SetupEnumOptions<TEnum>(this TMP_Dropdown dropdown) 
            where TEnum : Enum
        {
            dropdown.ClearOptions();
            var names = Enum.GetNames(typeof(TEnum));
            dropdown.AddOptions(names.ToList());
        }

        public static void SetupAutosaveOptions(this TMP_Dropdown dropdown)
        {
            dropdown.ClearOptions();
            dropdown.AddOptions(new[]
            {
                "Отключено",
                "15 сек",
                "30 сек",
                "1 мин",
                "2 мин",
                "5 мин"
            }.ToList());
        }

        public static void SetupQualityOptions(this TMP_Dropdown dropdown)
        {
            dropdown.ClearOptions();
            dropdown.AddOptions(QualitySettings.names.ToList());
        }
    }

    public static class CollectionHelpers
    {
        public static int IndexOf<T>(this T[] array, T value)
        {
            return Array.IndexOf(array, value);
        }

        public static T GetValueOrDefault<T>(this T[] array, int index, T defaultValue = default)
        {
            return index >= 0 && index < array.Length ? array[index] : defaultValue;
        }
    }

    public static class ReactiveHelpers
    {
        public static Observable<T> WhereNotNull<T>(this Observable<T> source) 
            where T : class
        {
            return source.Where(x => x != null);
        }

        public static Observable<TResult> SelectNotNull<TSource, TResult>(
            this Observable<TSource> source,
            Func<TSource, TResult> selector) 
            where TSource : class
            where TResult : class
        {
            return source
                .Where(x => x != null)
                .Select(selector)
                .Where(x => x != null);
        }

        public static Observable<Unit> ToUnit<T>(this Observable<T> source)
        {
            return source.Select(_ => Unit.Default);
        }

        public static Observable<T> Throttle<T>(
            this Observable<T> source, 
            float seconds)
        {
            return source.ThrottleFirst(TimeSpan.FromSeconds(seconds));
        }

        public static Observable<T> Debounce<T>(
            this Observable<T> source,
            float seconds)
        {
            return source.Debounce(TimeSpan.FromSeconds(seconds));
        }
    }

    public static class ValidationHelpers
    {
        public static bool ValidateAndClamp(
            this TMP_InputField input,
            float min,
            float max,
            out float result)
        {
            if (float.TryParse(input.text, out result))
            {
                result = Mathf.Clamp(result, min, max);
                input.text = result.ToString("F2");
                return true;
            }
            result = min;
            return false;
        }

        public static bool ValidateAndClamp(
            this TMP_InputField input,
            int min,
            int max,
            out int result)
        {
            if (int.TryParse(input.text, out result))
            {
                result = Mathf.Clamp(result, min, max);
                input.text = result.ToString();
                return true;
            }
            result = min;
            return false;
        }

        public static IDisposable ValidateOnChange(
            this TMP_InputField input,
            Func<string, (bool isValid, string errorMessage)> validator,
            TextMeshProUGUI errorText)
        {
            input.onValueChanged.AddListener(value =>
            {
                var (isValid, errorMessage) = validator(value);
                if (errorText != null)
                {
                    errorText.text = isValid ? "" : errorMessage;
                    errorText.gameObject.SetActive(!isValid);
                }
            });

            return Disposable.Create(() => input.onValueChanged.RemoveAllListeners());
        }
    }

    public static class LayoutHelpers
    {
        public static void RefreshLayout(this RectTransform rectTransform)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
        }

        public static void FitContentSize(this ScrollRect scrollRect)
        {
            Canvas.ForceUpdateCanvases();
            scrollRect.verticalNormalizedPosition = 1f;
        }
    }
}