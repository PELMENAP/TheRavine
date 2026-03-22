using System.Text;
public static class ItemPromptBuilder
{
    public static string Build(ItemContext item, PlayerContext player, float expertise, float doubt)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("[КОНТЕКСТ]");
        sb.AppendLine($"предмет: {item.ItemName}");
        sb.AppendLine($"теги: {string.Join(", ", item.ItemTags)}");
        sb.AppendLine($"материал: {item.Material}");
        sb.AppendLine($"профессия_игрока: {player.ProfessionId}");
        sb.AppendLine($"уровень_знаний: {GetExpertiseLabel(expertise)}");
        
        if (player.KnownFacts.Length > 0)
            sb.AppendLine($"факты_о_персонаже: {string.Join("; ", player.KnownFacts)}");
        
        sb.AppendLine();
        sb.AppendLine("[ЗАДАЧА]");
        sb.AppendLine(GetTaskByExpertise(expertise));
        sb.AppendLine(GetQuestions(doubt));
        
        sb.AppendLine();
        sb.AppendLine("[ФОРМАТ]");
        sb.AppendLine("Выведи описание предмета 2-3 предложения. Без кавычек.");
        
        return sb.ToString();
    }

    static string GetExpertiseLabel(float e) => e switch
    {
        >= 0.8f => "эксперт",
        >= 0.5f => "знаком поверхностно",
        >= 0.2f => "слышал краем уха",
        _       => "полный профан"
    };

    static string GetTaskByExpertise(float e) => e switch
    {
        >= 0.8f => "Опиши предмет точно и профессионально, замечая детали которые видит только специалист.",
        >= 0.5f => "Опиши предмет, упомяни что-то верное и что-то приблизительное.",
        >= 0.2f => "Опиши предмет с ошибками и домыслами, но уверенно.",
        _       => "Опиши предмет абсурдно неправильно, как человек который никогда не видел ничего подобного."
    };

    static string GetQuestions(float e) => e switch
    {
        >= 0.8f => "Не говори ничего лишнего, только сухие факты о предмете",
        >= 0.5f => "Покажи, что немного сомневаешься в своих знаниях о предмете",
        >= 0.2f => "Задавай вопросы зачем и для чего можно использовать данный предмет",
        _       => "Задавайся в каждом предложении вопросами о смысле существования"
    };
}