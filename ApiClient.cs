// =============================================================
//  ApiClient.cs — Python FastAPI 서버 HTTP 통신
//  담당: 지원
//  설명: localhost:8000 에서 실행 중인 FastAPI 서버와 통신합니다.
//        서버 미실행 상태의 예외를 안전하게 처리합니다.
// =============================================================

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using KWUStudentManager.Models;
using Newtonsoft.Json;

namespace KWUStudentManager
{
    /// <summary>
    /// Python FastAPI 서버(localhost:8000)와 HTTP로 통신하는 클라이언트입니다.
    /// ApiClient.Instance 싱글턴 또는 인스턴스를 직접 생성해 사용하세요.
    /// </summary>
    public class ApiClient : IDisposable
    {
        // ── 상수 ──────────────────────────────────────────────────
        // Python FastAPI 서버 기본 주소
        private const string BASE_URL = "http://localhost:8000";

        // 요청 타임아웃 (초) — AI 생성은 시간이 걸릴 수 있으므로 넉넉하게 설정
        private const int TIMEOUT_SECONDS = 30;

        // HttpClient는 재사용이 권장됩니다 (소켓 고갈 방지)
        private readonly HttpClient _httpClient;

        // ── 싱글턴 ────────────────────────────────────────────────
        // 앱 전역에서 공유하는 싱글턴 인스턴스
        // 사용법: ApiClient.Instance.GetTodosAsync()
        private static ApiClient _instance;
        public static ApiClient Instance => _instance ?? (_instance = new ApiClient());

        // ── 생성자 ────────────────────────────────────────────────
        // HttpClient 초기화 및 기본 헤더 설정
        public ApiClient()
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(BASE_URL),
                Timeout     = TimeSpan.FromSeconds(TIMEOUT_SECONDS)
            };
            // JSON 응답 수신 명시
            _httpClient.DefaultRequestHeaders.Accept.Add(
                new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json")
            );
        }

        // ── TODO API ──────────────────────────────────────────────

        // [GET /todos/generate]
        // 크롤링 + LLM 분석으로 AI가 생성한 TODO 목록을 가져옵니다.
        // AI 처리 시간이 있으므로 타임아웃이 길게 설정되어 있습니다.
        public async Task<List<TodoItem>> GetAIGeneratedTodosAsync()
        {
            return await GetAsync<List<TodoItem>>("/todos/generate", "AI TODO 생성");
        }

        // [GET /todos]
        // 서버에 저장된 TODO 전체 목록을 가져옵니다.
        public async Task<List<TodoItem>> GetTodosAsync()
        {
            return await GetAsync<List<TodoItem>>("/todos", "TODO 목록 조회");
        }

        // [POST /todos]
        // 새로운 TODO 항목을 서버에 추가합니다.
        // Id는 서버에서 자동 부여됩니다.
        public async Task<bool> AddTodoAsync(TodoItem todo)
        {
            return await PostAsync("/todos", todo, "TODO 추가");
        }

        // [PUT /todos/{id}]
        // 기존 TODO 항목을 수정합니다. (완료 체크, 내용 변경 등)
        // todo.Id 필드로 대상 항목을 식별합니다.
        public async Task<bool> UpdateTodoAsync(TodoItem todo)
        {
            return await PutAsync(string.Format("/todos/{0}", todo.Id), todo, "TODO 수정");
        }

        // [DELETE /todos/{id}]
        // 특정 TODO 항목을 서버에서 삭제합니다.
        public async Task<bool> DeleteTodoAsync(int id)
        {
            return await DeleteAsync(string.Format("/todos/{0}", id), "TODO 삭제");
        }

        // ── 스케줄 API ────────────────────────────────────────────

        // [GET /schedules]
        // 크롤링된 학사 일정 목록을 가져옵니다.
        public async Task<List<Dictionary<string, string>>> GetSchedulesAsync()
        {
            return await GetAsync<List<Dictionary<string, string>>>(
                "/schedules", "학사 일정 조회");
        }

        // ── 서버 상태 확인 ────────────────────────────────────────
        // Python 서버가 실행 중인지 확인합니다.
        // Form_Load나 버튼 클릭 전에 사전 체크용으로 사용하세요.
        public async Task<bool> IsServerRunningAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("/todos");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                // 연결 실패 = 서버 미실행
                return false;
            }
        }

        // ── 내부 공통 HTTP 핸들러 ─────────────────────────────────

        // GET 요청 공통 처리 메서드
        private async Task<T> GetAsync<T>(string endpoint, string actionName) where T : new()
        {
            try
            {
                string json = await _httpClient.GetStringAsync(endpoint);
                return JsonConvert.DeserializeObject<T>(json) ?? new T();
            }
            catch (HttpRequestException)
            {
                // 서버 미실행 또는 네트워크 오류
                ShowServerOfflineMessage(actionName);
                return new T();
            }
            catch (TaskCanceledException)
            {
                // 타임아웃
                ShowTimeoutMessage(actionName);
                return new T();
            }
            catch (JsonException ex)
            {
                // 서버 응답 형식 불일치
                MessageBox.Show(
                    string.Format("서버 응답을 파싱하는 중 오류가 발생했습니다.\n작업: {0}\n오류: {1}",
                        actionName, ex.Message),
                    "파싱 오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return new T();
            }
            catch (Exception ex)
            {
                ShowGenericErrorMessage(actionName, ex);
                return new T();
            }
        }

        // POST 요청 공통 처리 메서드
        private async Task<bool> PostAsync<T>(string endpoint, T data, string actionName)
        {
            try
            {
                string json     = JsonConvert.SerializeObject(data);
                var    content  = new StringContent(json, Encoding.UTF8, "application/json");
                var    response = await _httpClient.PostAsync(endpoint, content);
                return response.IsSuccessStatusCode;
            }
            catch (HttpRequestException) { ShowServerOfflineMessage(actionName); return false; }
            catch (TaskCanceledException) { ShowTimeoutMessage(actionName);      return false; }
            catch (Exception ex)         { ShowGenericErrorMessage(actionName, ex); return false; }
        }

        // PUT 요청 공통 처리 메서드
        private async Task<bool> PutAsync<T>(string endpoint, T data, string actionName)
        {
            try
            {
                string json     = JsonConvert.SerializeObject(data);
                var    content  = new StringContent(json, Encoding.UTF8, "application/json");
                var    response = await _httpClient.PutAsync(endpoint, content);
                return response.IsSuccessStatusCode;
            }
            catch (HttpRequestException) { ShowServerOfflineMessage(actionName); return false; }
            catch (TaskCanceledException) { ShowTimeoutMessage(actionName);      return false; }
            catch (Exception ex)         { ShowGenericErrorMessage(actionName, ex); return false; }
        }

        // DELETE 요청 공통 처리 메서드
        private async Task<bool> DeleteAsync(string endpoint, string actionName)
        {
            try
            {
                var response = await _httpClient.DeleteAsync(endpoint);
                return response.IsSuccessStatusCode;
            }
            catch (HttpRequestException) { ShowServerOfflineMessage(actionName); return false; }
            catch (TaskCanceledException) { ShowTimeoutMessage(actionName);      return false; }
            catch (Exception ex)         { ShowGenericErrorMessage(actionName, ex); return false; }
        }

        // ── 오류 메시지 헬퍼 ──────────────────────────────────────

        // 서버 미실행 시 사용자에게 안내 메시지를 표시합니다.
        private static void ShowServerOfflineMessage(string actionName)
        {
            MessageBox.Show(
                string.Format(
                    "Python 서버에 연결할 수 없습니다.\n\n" +
                    "작업: {0}\n서버 주소: {1}\n\n" +
                    "server.py가 실행 중인지 확인하세요.\n" +
                    "→ 터미널에서: uvicorn server:app --reload",
                    actionName, BASE_URL),
                "서버 연결 실패", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        // 요청 타임아웃 시 사용자에게 안내 메시지를 표시합니다.
        private static void ShowTimeoutMessage(string actionName)
        {
            MessageBox.Show(
                string.Format(
                    "서버 응답 시간이 초과되었습니다. ({0}초)\n\n" +
                    "작업: {1}\n\n" +
                    "AI TODO 생성은 시간이 더 걸릴 수 있습니다.\n" +
                    "서버 상태를 확인하고 다시 시도해 주세요.",
                    TIMEOUT_SECONDS, actionName),
                "응답 시간 초과", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        // 기타 예외 메시지를 표시합니다.
        private static void ShowGenericErrorMessage(string actionName, Exception ex)
        {
            MessageBox.Show(
                string.Format("예상치 못한 오류가 발생했습니다.\n작업: {0}\n오류: {1}",
                    actionName, ex.Message),
                "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        // ── IDisposable ───────────────────────────────────────────
        // HttpClient 리소스를 해제합니다.
        // 앱 종료 시 호출되거나 using 블록에서 자동 해제됩니다.
        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
