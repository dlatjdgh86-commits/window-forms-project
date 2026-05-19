// =============================================================
//  MainForm_Integration.cs
//  ── MainForm.cs 에 붙여 넣을 통합 연동 예시 + 테스트 코드
//
//  ※ 이 파일 자체를 프로젝트에 추가하지 말고,
//     해당 코드 블록을 MainForm.cs 안에 복사해 넣으세요.
// =============================================================

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;
using KWUStudentManager;
using KWUStudentManager.Models;

namespace KWUStudentManager
{
    public partial class MainForm : Form
    {
        // 현재 화면에 표시 중인 TODO 목록
        private List<TodoItem> _todoList = new List<TodoItem>();

        // ════════════════════════════════════════════════════════
        //  1. Form_Load — 앱 시작 시 실행
        // ════════════════════════════════════════════════════════
        private async void MainForm_Load(object sender, EventArgs e)
        {
            // ① DataManager 초기화 — 저장 폴더 생성 (최초 1회)
            DataManager.Initialize();

            // ② 로컬 저장 데이터 우선 로드 (서버 없어도 기존 TODO 표시)
            _todoList = DataManager.Load<TodoItem>(DataManager.FileName.Todos);
            _todoList.ForEach(t => t.RecalculateDDay()); // D-Day 재계산
            RefreshTodoListUI();
            SyncCalendarDots();

            // ③ 서버 연결 가능 여부 확인 후 최신 데이터 동기화
            bool serverOnline = await ApiClient.Instance.IsServerRunningAsync();
            if (serverOnline)
            {
                await SyncWithServerAsync();
            }
            else
            {
                // 상태 바로 오프라인 표시 (toolStripStatusLabel 컨트롤 필요)
                // toolStripStatusLabel.Text = "⚠️ Python 서버 오프라인 — 로컬 데이터 사용 중";
            }
        }

        // ════════════════════════════════════════════════════════
        //  2. AI TODO 생성 버튼 클릭
        // ════════════════════════════════════════════════════════
        private async void btnGenerateAI_Click(object sender, EventArgs e)
        {
            // 중복 클릭 방지 + 로딩 표시
            btnGenerateAI.Enabled = false;
            btnGenerateAI.Text    = "⏳ AI 분석 중...";

            try
            {
                var aiTodos = await ApiClient.Instance.GetAIGeneratedTodosAsync();

                if (aiTodos == null || aiTodos.Count == 0)
                {
                    MessageBox.Show("AI가 생성한 TODO가 없습니다.\n학사 일정을 확인해 주세요.",
                        "결과 없음", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // 기존 TODO에 AI 생성 항목 병합 (중복 제목 방지)
                foreach (var aiItem in aiTodos)
                {
                    if (!_todoList.Exists(t => t.Title == aiItem.Title))
                        _todoList.Add(aiItem);
                }

                // 로컬 저장 + UI 갱신
                DataManager.Save(DataManager.FileName.Todos, _todoList);
                RefreshTodoListUI();
                SyncCalendarDots();

                MessageBox.Show(string.Format("✅ {0}개의 AI TODO가 추가되었습니다.", aiTodos.Count),
                    "생성 완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            finally
            {
                // 작업 완료 후 버튼 복구
                btnGenerateAI.Enabled = true;
                btnGenerateAI.Text    = "🤖 AI TODO 생성";
            }
        }

        // ════════════════════════════════════════════════════════
        //  3. TODO 완료 체크 처리
        // ════════════════════════════════════════════════════════
        private async void OnTodoCompleted(TodoItem todo)
        {
            todo.IsCompleted = !todo.IsCompleted;
            DataManager.Save(DataManager.FileName.Todos, _todoList);
            await ApiClient.Instance.UpdateTodoAsync(todo); // 서버 동기화 (실패해도 로컬 유지)
            RefreshTodoListUI();
            SyncCalendarDots();
        }

        // ════════════════════════════════════════════════════════
        //  4. TODO 삭제 처리
        // ════════════════════════════════════════════════════════
        private async void OnTodoDeleted(int todoId)
        {
            var result = MessageBox.Show("이 TODO 를 삭제하시겠습니까?",
                "삭제 확인", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result != DialogResult.Yes) return;

            _todoList.RemoveAll(t => t.Id == todoId);
            DataManager.Save(DataManager.FileName.Todos, _todoList);
            await ApiClient.Instance.DeleteTodoAsync(todoId);
            RefreshTodoListUI();
            SyncCalendarDots();
        }

        // ════════════════════════════════════════════════════════
        //  5. 서버 동기화 헬퍼
        // ════════════════════════════════════════════════════════
        private async Task SyncWithServerAsync()
        {
            var serverTodos = await ApiClient.Instance.GetTodosAsync();
            if (serverTodos == null || serverTodos.Count == 0) return;

            // 서버 데이터를 기준으로 덮어쓰기 (서버가 Source of Truth)
            _todoList = serverTodos;
            _todoList.ForEach(t => t.RecalculateDDay()); // 오늘 기준 재계산
            DataManager.Save(DataManager.FileName.Todos, _todoList);
            RefreshTodoListUI();
            SyncCalendarDots();
        }

        // ════════════════════════════════════════════════════════
        //  6. UI 갱신 헬퍼
        // ════════════════════════════════════════════════════════
        // _todoList 기준으로 TODO 패널을 다시 그립니다.
        // 우선순위 순 (높음 → 보통 → 낮음) + D-Day 오름차순 정렬
        private void RefreshTodoListUI()
        {
            var displayList = _todoList.FindAll(t => !t.IsCompleted);
            displayList.Sort((a, b) =>
            {
                int p = PriorityOrder(a.Priority) - PriorityOrder(b.Priority);
                return p != 0 ? p : a.DDay.CompareTo(b.DDay);
            });

            // 여기서 TodoCard 컨트롤을 동적으로 생성해 todoPanel에 추가하세요.
            // todoPanel.Controls.Clear();
            // foreach (var todo in displayList)
            //     todoPanel.Controls.Add(new TodoCard(todo, OnTodoCompleted, OnTodoDeleted));
        }

        // 우선순위 정렬 숫자 반환 (낮을수록 위)
        private static int PriorityOrder(string priority)
        {
            switch (priority)
            {
                case "높음": return 0;
                case "보통": return 1;
                case "낮음": return 2;
                default:     return 3;
            }
        }

        // DueDate가 있는 TODO를 MonthCalendar 굵은 날짜로 표시합니다.
        private void SyncCalendarDots()
        {
            var dueDates = new List<DateTime>();
            foreach (var t in _todoList)
                if (t.DueDate.HasValue && !t.IsCompleted)
                    dueDates.Add(t.DueDate.Value.Date);

            // monthCalendar1 컨트롤이 MainForm에 추가되어 있어야 합니다.
            // monthCalendar1.BoldedDates = dueDates.ToArray();
            // monthCalendar1.UpdateBoldedDates();
        }
    }
}


// ════════════════════════════════════════════════════════════
//  ★ 독립 실행 테스트 스니펫
//  ── 별도 버튼 클릭 이벤트나 콘솔 프로젝트에서 실행 가능
// ════════════════════════════════════════════════════════════

/*
──────────────────────────────────────────────
 [테스트 A] DataManager 단독 테스트 (서버 불필요)
──────────────────────────────────────────────
 private void btnTestDataManager_Click(object sender, EventArgs e)
 {
     DataManager.Initialize();
     Console.WriteLine(DataManager.GetStorageInfo());

     // 샘플 데이터 저장
     var list = new List<TodoItem>
     {
         TodoItem.Create("알고리즘 중간고사 준비", "높음",
             DateTime.Today.AddDays(3),  "3일 후 시험",  isAI: true),
         TodoItem.Create("소공 팀과제 초안",       "보통",
             DateTime.Today.AddDays(7),  "7일 후 마감",  isAI: true),
         TodoItem.Create("선형대수 복습",          "낮음",
             DateTime.Today.AddDays(14), "여유 있음",    isAI: false),
     };
     for (int i = 0; i < list.Count; i++) list[i].Id = i + 1;

     bool saved = DataManager.Save(DataManager.FileName.Todos, list);
     Console.WriteLine("저장: " + saved);

     var loaded = DataManager.Load<TodoItem>(DataManager.FileName.Todos);
     Console.WriteLine("불러온 수: " + loaded.Count);
     foreach (var t in loaded) Console.WriteLine("  " + t);

     // Upsert: 첫 번째 항목 완료 처리
     loaded[0].IsCompleted = true;
     DataManager.Upsert(DataManager.FileName.Todos, loaded[0], t => t.Id);
     Console.WriteLine("완료 처리 후: " +
         DataManager.Load<TodoItem>(DataManager.FileName.Todos)[0].IsCompleted);

     // Delete: Id=2 삭제
     DataManager.Delete<TodoItem>(DataManager.FileName.Todos, 2, t => t.Id);
     Console.WriteLine("삭제 후 수: " +
         DataManager.Load<TodoItem>(DataManager.FileName.Todos).Count);
 }

──────────────────────────────────────────────
 [테스트 B] ApiClient 단독 테스트 (서버 필요)
──────────────────────────────────────────────
 private async void btnTestApiClient_Click(object sender, EventArgs e)
 {
     bool online = await ApiClient.Instance.IsServerRunningAsync();
     MessageBox.Show("서버 상태: " + (online ? "온라인" : "오프라인"));
     if (!online) return;

     var todos  = await ApiClient.Instance.GetTodosAsync();
     Console.WriteLine("서버 TODO 수: " + todos.Count);

     var newTodo = TodoItem.Create("테스트 TODO", "보통",
         DateTime.Today.AddDays(5), "테스트용");
     bool added  = await ApiClient.Instance.AddTodoAsync(newTodo);
     Console.WriteLine("추가: " + added);

     newTodo.Id = 1; // 실제로는 서버 응답 Id 사용
     newTodo.IsCompleted = true;
     bool updated = await ApiClient.Instance.UpdateTodoAsync(newTodo);
     Console.WriteLine("수정: " + updated);

     bool deleted = await ApiClient.Instance.DeleteTodoAsync(newTodo.Id);
     Console.WriteLine("삭제: " + deleted);
 }

──────────────────────────────────────────────
 [테스트 C] 통합 테스트 (DataManager + ApiClient)
──────────────────────────────────────────────
 private async void btnTestIntegration_Click(object sender, EventArgs e)
 {
     DataManager.Initialize();

     // 1) AI TODO 생성 요청
     var aiTodos = await ApiClient.Instance.GetAIGeneratedTodosAsync();
     Console.WriteLine("AI 생성: " + aiTodos.Count + "개");

     // 2) DDay 재계산 (오늘 날짜 기준)
     aiTodos.ForEach(t => t.RecalculateDDay());

     // 3) 로컬 저장
     DataManager.Save(DataManager.FileName.Todos, aiTodos);

     // 4) 다시 불러와서 검증
     var loaded = DataManager.Load<TodoItem>(DataManager.FileName.Todos);
     Console.WriteLine("로컬 저장 확인: " + loaded.Count + "개");
     foreach (var t in loaded)
         Console.WriteLine(string.Format("  [{0}] {1} ({2}) — {3}",
             t.Priority, t.Title, t.DDayLabel, t.Reason));
 }
*/
