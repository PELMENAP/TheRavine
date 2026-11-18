public static class CodexErrorText
{
    public static string Message(CodexError error) => error switch
    {
        CodexError.None => "Готово",
        CodexError.NotAuthority => "Законы меняет только мир-хозяин, не клиент общей сессии",
        CodexError.NoPurse => "Нет автора закона: существо не найдено или мертво",
        CodexError.BadValue => "Значение не число или вне допустимого вида",
        CodexError.EmptyLaw => "Закон ничего не меняет: все сдвиги равны нулю",
        CodexError.TooManyAxes => "Слишком много осей в одном законе",
        CodexError.DeltaTooLarge => "Сдвиг оси больше допустимого за один закон",
        CodexError.OutOfBounds => "Сдвиг выходит за конституционные границы оси",
        CodexError.Inadmissible => "Закон даёт всем бесплатную энергию: уравновесь его противоположным сдвигом",
        CodexError.CodexFull => "Свод законов заполнен: дождись, пока слабые законы отомрут",
        CodexError.NoSuchLaw => "Такого закона нет",
        CodexError.BadAxis => "Такой оси нет",
        CodexError.InsufficientEnergy => "Не хватает энергии",
        CodexError.EnergyTooSmall => "Вклад меньше минимального",
        _ => "Неизвестная ошибка"
    };
}
