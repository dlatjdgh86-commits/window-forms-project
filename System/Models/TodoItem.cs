// =============================================================
//  TodoItem.cs — TODO 항목 데이터 모델
//  담당: 지원
//  설명: Python FastAPI 서버 및 JSON 저장과 공통으로 사용하는
//        핵심 데이터 클래스입니다.
// =============================================================

using System;
using Newtonsoft.Json;

namespace KWUStudentManager.Models
{
    /// <summary>
    /// TODO 항목 하나를 나타내는 데이터 모델입니다.
    /// JSON 직렬화/역직렬화 및 API 응답 파싱에 공통 사용됩니다.
    /// </summary>
    public class TodoItem
    {
        // ── 기본 식별자 ───────────────────────────────────────────
        // TODO 고유 ID (로컬 저장 및 서버 식별자)
        [JsonProperty("id")]
        public int Id { get; set; }

        // ── 내용 필드 ─────────────────────────────────────────────
        // TODO 제목 (예: "알고리즘 중간고사 준비")
        [JsonProperty("title")]
        public string Title { get; set; }

        // 우선순위: "높음" / "보통" / "낮음" 세 값만 허용됩니다.
        [JsonProperty("priority")]
        public string Priority { get; set; }

        // 마감일 (없을 수 있으므로 Nullable)
        [JsonProperty("dueDate")]
        public DateTime? DueDate { get; set; }

        // 마감까지 남은 일수 (D-Day 값, 오늘=0, 지남=음수)
        [JsonProperty("dDay")]
        public int DDay { get; set; }

        // AI가 이 TODO를 생성한 이유 (예: "3일 후 시험, 즉시 시작 필요")
        [JsonProperty("reason")]
        public string Reason { get; set; }

        // ── 상태 플래그 ───────────────────────────────────────────
        // 완료 여부 (체크박스 상태와 연동)
        [JsonProperty("isCompleted")]
        public bool IsCompleted { get; set; }

        // AI 자동 생성 여부 (true → AI 뱃지 표시)
        [JsonProperty("isAIGenerated")]
        public bool IsAIGenerated { get; set; }

        // ── 편의 속성 (JSON 저장 제외) ────────────────────────────
        // D-Day 표시 문자열 (예: "D-3", "D-Day", "D+1")
        // JSON 저장 제외, UI 바인딩 시 직접 계산합니다.
        [JsonIgnore]
        public string DDayLabel
        {
            get
            {
                if (DDay == 0) return "D-Day";
                if (DDay > 0)  return string.Format("D-{0}", DDay);
                return string.Format("D+{0}", Math.Abs(DDay)); // 마감 지난 경우
            }
        }

        // 우선순위 이모지 표시 문자열
        [JsonIgnore]
        public string PriorityLabel
        {
            get
            {
                switch (Priority)
                {
                    case "높음": return "🔴 높음";
                    case "보통": return "🟡 보통";
                    case "낮음": return "🟢 낮음";
                    default:     return "⚪ 미분류";
                }
            }
        }

        // ── 편의 메서드 ───────────────────────────────────────────
        // DueDate 기준으로 DDay를 오늘 날짜에 맞게 재계산합니다.
        // 저장된 데이터를 불러올 때 호출하세요.
        public void RecalculateDDay()
        {
            if (DueDate.HasValue)
                DDay = (DueDate.Value.Date - DateTime.Today).Days;
        }

        // 새 TodoItem을 생성하는 정적 팩토리 메서드
        public static TodoItem Create(string title, string priority,
            DateTime? dueDate, string reason = "", bool isAI = false)
        {
            var item = new TodoItem
            {
                Title         = title,
                Priority      = priority,
                DueDate       = dueDate,
                Reason        = reason,
                IsCompleted   = false,
                IsAIGenerated = isAI,
            };
            item.RecalculateDDay();
            return item;
        }

        // 디버그용 문자열 표현
        public override string ToString()
        {
            return string.Format("[{0}] {1} ({2})", PriorityLabel, Title, DDayLabel);
        }
    }
}
