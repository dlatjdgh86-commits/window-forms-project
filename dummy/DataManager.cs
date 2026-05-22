using System.Text.Json;

namespace KwuTodoAI
{
    /// <summary>
    /// TODO 데이터를 관리하는 클래스
    /// - 1단계: GetDummyTodos()로 더미 데이터 제공 (서버 없이 화면 테스트용)
    /// - 2단계: SaveToFile() / LoadFromFile()로 JSON 파일 저장/불러오기
    /// </summary>
    public static class DataManager
    {
        // JSON 파일이 저장될 경로 (실행파일과 같은 폴더)
        private static readonly string SavePath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "todos.json");

        // ─────────────────────────────────────────────────────────
        //  1단계: 더미 데이터 (서버 없이 화면 테스트용)
        // ─────────────────────────────────────────────────────────
        public static List<TodoItem> GetDummyTodos()
        {
            var today    = DateTime.Today;
            var tomorrow = today.AddDays(1);
            var in3days  = today.AddDays(3);
            var in7days  = today.AddDays(7);
            var in14days = today.AddDays(14);

            return new List<TodoItem>
            {
                // D-1 (빨간색) — 내일 마감
                new TodoItem
                {
                    Id          = 1,
                    Title       = "알고리즘 과제 제출",
                    Priority    = "긴급",
                    DueDate     = tomorrow.ToString("yyyy-MM-dd"),
                    DDay        = 1,
                    Type        = "과제",
                    Reason      = "내일까지 제출해야 하는 알고리즘 과제입니다.",
                    ActionItems = new List<string> { "정렬 알고리즘 구현", "시간복잡도 분석 작성" },
                    IsTeamWork  = false,
                    Completed   = false,
                    Source      = "dummy"
                },
                // D-3 (노란색) — 3일 후 마감
                new TodoItem
                {
                    Id          = 2,
                    Title       = "데이터베이스 팀 프로젝트",
                    Priority    = "높음",
                    DueDate     = in3days.ToString("yyyy-MM-dd"),
                    DDay        = 3,
                    Type        = "과제",
                    Reason      = "DB 스키마 설계 및 SQL 쿼리 작성 마감",
                    ActionItems = new List<string> { "ERD 다이어그램 완성", "SQL 쿼리 작성", "팀원 코드 리뷰" },
                    IsTeamWork  = true,
                    Completed   = false,
                    Source      = "dummy"
                },
                // D-7 (초록색) — 일주일 후 마감
                new TodoItem
                {
                    Id          = 3,
                    Title       = "소프트웨어공학 중간고사",
                    Priority    = "높음",
                    DueDate     = in7days.ToString("yyyy-MM-dd"),
                    DDay        = 7,
                    Type        = "시험",
                    Reason      = "소프트웨어 개발 방법론 전 범위 시험",
                    ActionItems = new List<string> { "UML 다이어그램 복습", "애자일 방법론 정리" },
                    IsTeamWork  = false,
                    Completed   = false,
                    Source      = "dummy"
                },
                // D-14 (회색) — 2주 후
                new TodoItem
                {
                    Id          = 4,
                    Title       = "윈폼 프로젝트 최종 발표",
                    Priority    = "보통",
                    DueDate     = in14days.ToString("yyyy-MM-dd"),
                    DDay        = 14,
                    Type        = "발표",
                    Reason      = "응용소프트웨어실습 최종 발표",
                    ActionItems = new List<string> { "PPT 준비", "데모 영상 녹화", "발표 연습" },
                    IsTeamWork  = true,
                    Completed   = false,
                    Source      = "dummy"
                },
                // 오늘 마감 D-DAY (빨간색)
                new TodoItem
                {
                    Id          = 5,
                    Title       = "수강신청 확인",
                    Priority    = "긴급",
                    DueDate     = today.ToString("yyyy-MM-dd"),
                    DDay        = 0,
                    Type        = "학사행정",
                    Reason      = "오늘까지 수강신청 정정 기간",
                    ActionItems = new List<string> { "수강신청 시스템 접속", "정정 신청 완료" },
                    IsTeamWork  = false,
                    Completed   = false,
                    Source      = "dummy"
                },
            };
        }

        // ─────────────────────────────────────────────────────────
        //  2단계: JSON 파일로 저장
        // ─────────────────────────────────────────────────────────
        public static void SaveToFile(List<TodoItem> todos)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(todos, options);
                File.WriteAllText(SavePath, json, System.Text.Encoding.UTF8);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"저장 실패: {ex.Message}", "오류",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ─────────────────────────────────────────────────────────
        //  2단계: JSON 파일에서 불러오기
        // ─────────────────────────────────────────────────────────
        public static List<TodoItem> LoadFromFile()
        {
            try
            {
                if (!File.Exists(SavePath))
                    return new List<TodoItem>();   // 파일 없으면 빈 리스트

                string json = File.ReadAllText(SavePath, System.Text.Encoding.UTF8);
                var result  = JsonSerializer.Deserialize<List<TodoItem>>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return result ?? new List<TodoItem>();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"불러오기 실패: {ex.Message}", "오류",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return new List<TodoItem>();
            }
        }

        // ─────────────────────────────────────────────────────────
        //  편의 메서드: 저장된 파일이 있으면 파일에서, 없으면 더미로
        // ─────────────────────────────────────────────────────────
        public static List<TodoItem> LoadOrDummy()
        {
            var saved = LoadFromFile();
            return saved.Count > 0 ? saved : GetDummyTodos();
        }

        // 저장 파일 경로 반환 (디버그용)
        public static string GetSavePath() => SavePath;
    }
}
