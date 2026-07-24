using System;
using UnityEngine;

namespace TheRavine.Generator
{
    [Serializable]
    public struct ErosionSettings
    {
        public bool allowInfiniteErosionDepth;

        [Tooltip("Количество капель воды. Для карты 256x256: 5000 - 20000.")]
        [Range(100, 50000)]
        public int dropletCount;

        [Tooltip("Максимальная жизнь капли в шагах.")]
        [Range(4, 256)]
        public int lifetime;

        [Tooltip("Множитель применения дельты эрозии к карте высот.")]
        [Range(0.1f, 10f)]
        public float amplify;

        [Tooltip("Инерция: 0 - капля всегда течет по градиенту вниз, 1 - капля летит по прямой и игнорирует рельеф.")]
        [Range(0f, 1f)]
        public float inertia;

        [Tooltip("Гравитация: насколько сильно падение высоты ускоряет каплю.")]
        [Range(0.01f, 10f)]
        public float gravity;

        [Tooltip("Доля воды, испаряющаяся за ОДИН шаг (0.01 = 1%). Не ставь больше 0.1!")]
        [Range(0f, 0.2f)]
        public float evaporation;

        [Tooltip("Вместимость осадка. Сколько земли может нести капля в зависимости от скорости и угла.")]
        [Range(0.01f, 32f)]
        public float sedimentCapacity;

        [Tooltip("Скорость осаждения земли, если капля переполнена.")]
        [Range(0f, 1f)]
        public float depositSpeed;

        [Tooltip("Скорость разрушения земли, если у капли есть место для осадка.")]
        [Range(0f, 1f)]
        public float erodeSpeed;

        [Tooltip("Минимальный угол склона. Защищает ровные равнины от случайной эрозии.")]
        [Range(0.00001f, 0.5f)]
        public float minSlope;

        [Tooltip("Радиус кисти эрозии в пикселях.")]
        [Range(0, 8)]
        public float radius;
    }
}