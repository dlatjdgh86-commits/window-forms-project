// =============================================================
//  DataManager.cs — JSON 로컬 저장/불러오기
//  담당: 지원
//  설명: AppData\Roaming\KWUStudentManager\ 폴더에
//        todos.json / schedules.json / courses.json 을 관리합니다.
//        첫 실행 시 폴더를 자동 생성합니다.
// =============================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace KWUStudentManager
{
    /// <summary>
    /// JSON 기반 로컬 데이터 저장/불러오기를 담당합니다.
    /// 모든 메서드는 static이므로 인스턴스 없이 바로 사용 가능합니다.
    /// </summary>
    public static class DataManager
    {
        // ── 경로 상수 ─────────────────────────────────────────────
        // 앱 데이터 저장 경로: C:\Users\{사용자명}\AppData\Roaming\KWUStudentManager\
        public static readonly string SaveFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "KWUStudentManager"
        );

        // 자주 쓰는 파일 이름 상수 (오탈자 방지)
        public static class FileName
        {
            public const string Todos     = "todos.json";
            public const string Schedules = "schedules.json";
            public const string Courses   = "courses.json";
        }

        // ── 초기화 ────────────────────────────────────────────────
        // 저장 폴더가 없으면 자동 생성합니다.
        // 앱 최초 실행 시 (Form_Load 등) 한 번만 호출하면 됩니다.
        public static void Initialize()
        {
            try
            {
                if (!Directory.Exists(SaveFolder))
                {
                    Directory.CreateDirectory(SaveFolder);
                    InitializeEmptyFiles(); // 빈 JSON 파일 사전 생성
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    string.Format(
                        "데이터 폴더 생성 중 오류가 발생했습니다.\n경로: {0}\n오류: {1}",
                        SaveFolder, ex.Message),
                    "초기화 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // 신규 설치 시 빈 JSON 파일을 미리 만들어 Load 파싱 오류를 방지합니다.
        private static void InitializeEmptyFiles()
        {
            string emptyJson = "[]";
            foreach (var name in new[] { FileName.Todos, FileName.Schedules, FileName.Courses })
            {
                string path = Path.Combine(SaveFolder, name);
                if (!File.Exists(path))
                    File.WriteAllText(path, emptyJson, Encoding.UTF8);
            }
        }

        // ── 저장 ──────────────────────────────────────────────────
        // 제네릭 리스트를 JSON 파일로 저장합니다.
        // fileName: "todos.json" 등 파일 이름만 전달 (경로 제외)
        // 반환값: 저장 성공 여부
        public static bool Save<T>(string fileName, List<T> data)
        {
            try
            {
                // 폴더가 없으면 다시 생성 (외부 삭제 대비)
                if (!Directory.Exists(SaveFolder))
                    Directory.CreateDirectory(SaveFolder);

                string path = Path.Combine(SaveFolder, fileName);
                string json = JsonConvert.SerializeObject(data, Formatting.Indented);

                // BOM 없는 UTF-8로 저장 (Python 서버와 호환)
                File.WriteAllText(path, json,
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    string.Format("데이터 저장 중 오류가 발생했습니다.\n파일: {0}\n오류: {1}",
                        fileName, ex.Message),
                    "저장 오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
        }

        // ── 불러오기 ──────────────────────────────────────────────
        // JSON 파일을 읽어 제네릭 리스트로 반환합니다.
        // 파일이 없거나 파싱 실패 시 빈 리스트를 반환합니다.
        public static List<T> Load<T>(string fileName)
        {
            try
            {
                string path = Path.Combine(SaveFolder, fileName);

                // 파일 없음 = 첫 실행 정상 케이스
                if (!File.Exists(path)) return new List<T>();

                string json = File.ReadAllText(path, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(json)) return new List<T>();

                return JsonConvert.DeserializeObject<List<T>>(json) ?? new List<T>();
            }
            catch (JsonException ex)
            {
                // JSON 형식 깨짐 → 백업 후 초기화
                MessageBox.Show(
                    string.Format("'{0}' 파일이 손상되어 초기화합니다.\n오류: {1}",
                        fileName, ex.Message),
                    "데이터 오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                BackupAndReset(fileName);
                return new List<T>();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    string.Format("데이터 불러오기 중 오류가 발생했습니다.\n파일: {0}\n오류: {1}",
                        fileName, ex.Message),
                    "불러오기 오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return new List<T>();
            }
        }

        // ── 업서트 ────────────────────────────────────────────────
        // 파일에서 리스트를 읽어, Id가 같은 항목을 교체하거나 새로 추가합니다.
        public static bool Upsert<T>(string fileName, T item, Func<T, int> idSelector)
        {
            var list = Load<T>(fileName);
            int idx  = list.FindIndex(x => idSelector(x) == idSelector(item));

            if (idx >= 0) list[idx] = item; // 기존 항목 교체
            else          list.Add(item);   // 신규 추가

            return Save(fileName, list);
        }

        // 특정 Id의 항목을 파일에서 삭제합니다.
        public static bool Delete<T>(string fileName, int id, Func<T, int> idSelector)
        {
            var list    = Load<T>(fileName);
            int removed = list.RemoveAll(x => idSelector(x) == id);
            if (removed == 0) return false; // 삭제 대상 없음
            return Save(fileName, list);
        }

        // ── 유틸리티 ──────────────────────────────────────────────
        // 손상된 파일을 .bak으로 백업하고 빈 파일로 초기화합니다.
        private static void BackupAndReset(string fileName)
        {
            try
            {
                string path = Path.Combine(SaveFolder, fileName);
                string bak  = path + string.Format(".{0}.bak",
                    DateTime.Now.ToString("yyyyMMdd_HHmmss"));
                if (File.Exists(path)) File.Move(path, bak);
                File.WriteAllText(path, "[]", Encoding.UTF8);
            }
            catch { /* 백업 실패 무시 */ }
        }

        // 저장 폴더 상태를 디버그용 문자열로 반환합니다.
        public static string GetStorageInfo()
        {
            if (!Directory.Exists(SaveFolder)) return "저장 폴더 없음";
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(string.Format("저장 경로: {0}", SaveFolder));
            foreach (var file in Directory.GetFiles(SaveFolder, "*.json"))
            {
                var info = new FileInfo(file);
                sb.AppendLine(string.Format("  {0} ({1} bytes)", info.Name, info.Length));
            }
            return sb.ToString();
        }
    }
}
