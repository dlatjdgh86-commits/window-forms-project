using System.Text.Json.Serialization;

namespace KwuTodoAI
{
    public class TodoItem
    {
        [JsonPropertyName("id")]          public int    Id         { get; set; }
        [JsonPropertyName("title")]       public string Title      { get; set; } = "";
        [JsonPropertyName("priority")]    public string Priority   { get; set; } = "보통";
        [JsonPropertyName("dueDate")]     public string DueDate    { get; set; } = "";
        [JsonPropertyName("dDay")]        public int    DDay       { get; set; }
        [JsonPropertyName("type")]        public string Type       { get; set; } = "기타";
        [JsonPropertyName("reason")]      public string Reason     { get; set; } = "";
        [JsonPropertyName("actionItems")] public List<string> ActionItems { get; set; } = new();
        [JsonPropertyName("isTeamWork")]  public bool   IsTeamWork { get; set; }
        [JsonPropertyName("completed")]   public bool   Completed  { get; set; }
        [JsonPropertyName("source")]      public string Source     { get; set; } = "";

        public string DDayText =>
            DDay == 0 ? "D-DAY" : DDay > 0 ? $"D-{DDay}" : $"D+{Math.Abs(DDay)}";

        public string TypeIcon => Type switch
        {
            "시험"    => "📝",
            "과제"    => "📋",
            "발표"    => "🎤",
            "학사행정" => "🏫",
            _         => "📌"
        };

        public Color PriorityColor => Priority switch
        {
            "긴급" => Color.FromArgb(220, 60,  60),
            "높음" => Color.FromArgb(230, 130, 30),
            "보통" => Color.FromArgb(60,  140, 220),
            _      => Color.FromArgb(130, 140, 155),
        };
    }

    public class TodoResponse
    {
        [JsonPropertyName("todos")]      public List<TodoItem>  Todos      { get; set; } = new();
        [JsonPropertyName("summary")]    public string          Summary    { get; set; } = "";
        [JsonPropertyName("statistics")] public TodoStatistics  Statistics { get; set; } = new();
    }

    public class TodoStatistics
    {
        [JsonPropertyName("total")]       public int Total       { get; set; }
        [JsonPropertyName("urgent")]      public int Urgent      { get; set; }
        [JsonPropertyName("high")]        public int High        { get; set; }
        [JsonPropertyName("exams")]       public int Exams       { get; set; }
        [JsonPropertyName("assignments")] public int Assignments { get; set; }
    }
}
