using System.Drawing.Drawing2D;
using System.Net.Http;
using System.Text.Json;

namespace KwuTodoAI
{
    public partial class MainForm : Form
    {
        private bool      _isDark;
        private DateTime  _currentMonth = DateTime.Today;
        private DateTime? _selectedDate = DateTime.Today;
        private List<TodoItem>      _todos  = new();
        private List<AcademicEvent> _events = new();
        private readonly HttpClient _http   = new() { Timeout = TimeSpan.FromSeconds(60) };

        // ── 데이터 소스 모드 ──────────────────────────────────────
        // true  = 서버(python server.py) 사용
        // false = 로컬 DataManager 사용 (더미 or JSON 파일)
        private const bool USE_SERVER = false;

        // ── 색상 테마 ─────────────────────────────────────────────
        private Color BG       => _isDark ? Color.FromArgb(15, 18, 30)    : Color.FromArgb(242, 245, 255);
        private Color SURFACE  => _isDark ? Color.FromArgb(24, 28, 46)    : Color.White;
        private Color SURFACE2 => _isDark ? Color.FromArgb(32, 38, 60)    : Color.FromArgb(245, 248, 255);
        private Color ACCENT   => Color.FromArgb(82, 130, 255);
        private Color TEXT     => _isDark ? Color.FromArgb(218, 226, 255) : Color.FromArgb(25, 30, 60);
        private Color SUBTEXT  => _isDark ? Color.FromArgb(130, 145, 180) : Color.FromArgb(115, 126, 162);
        private Color URGENT   => Color.FromArgb(220, 60,  60);
        private Color HIGH_C   => Color.FromArgb(230, 130, 30);
        private Color SEL_DAY  => _isDark ? Color.FromArgb(60, 90, 180)   : Color.FromArgb(185, 215, 255);
        private Color SEL_TEXT => _isDark ? Color.White                   : Color.FromArgb(25, 30, 60);

        // ─────────────────────────────────────────────────────────
        //  생성자
        // ─────────────────────────────────────────────────────────
        public MainForm(bool isDark = false)
        {
            _isDark = isDark;
            InitializeComponent();
            HookEvents();
            lblDate.Text      = DateTime.Today.ToString("yyyy년 MM월 dd일 (ddd)");
            lblMonth.Text     = _currentMonth.ToString("yyyy년 MM월");
            lblSchedDate.Text = $"📅 {DateTime.Today:M월 d일} 일정";
            ApplyTheme();
            RebuildCalendar();
            LoadTodosLocal();          // 시작 시 더미/저장 데이터 바로 로드
            LoadSampleAcademicEvents();
        }

        // ─────────────────────────────────────────────────────────
        //  이벤트 연결
        // ─────────────────────────────────────────────────────────
        private void HookEvents()
        {
            btnTheme.Click += (s, e) =>
            {
                _isDark = !_isDark;
                btnTheme.Text = _isDark ? "☀️" : "🌙";
                ApplyTheme();
            };

            // "AI 생성" 버튼: USE_SERVER 플래그에 따라 분기
            btnRefresh.Click += async (s, e) =>
            {
                if (USE_SERVER)
                    await LoadTodosFromServerAsync();
                else
                    LoadTodosLocal();
            };

            pnlHeader.Resize += (s, e) => PositionHeaderButtons();
            this.Load        += (s, e) => PositionHeaderButtons();

            btnPrev.Click += (s, e) => { _currentMonth = _currentMonth.AddMonths(-1); RebuildCalendar(); };
            btnNext.Click += (s, e) => { _currentMonth = _currentMonth.AddMonths(1);  RebuildCalendar(); };

            pnlLeft.Resize += (s, e) =>
            {
                btnNext.Location = new Point(pnlLeft.Width - 38, 10);
                btnPrev.Location = new Point(pnlLeft.Width - 70, 10);
            };

            pnlRight.Resize += (s, e) =>
            {
                int h = pnlRight.ClientSize.Height - 2;
                pnlRightTop.Height = (int)(h * 0.55);
            };

            pnlLoading.Resize += (s, e) => lblLoading.Location = new Point(
                (pnlLoading.Width  - lblLoading.PreferredWidth)  / 2,
                (pnlLoading.Height - lblLoading.PreferredHeight) / 2);

            pnlTodoTop.Resize += (s, e) =>
                btnRefresh.Location = new Point(pnlTodoTop.Width - btnRefresh.Width - 8, 6);

            // 폼 닫힐 때 자동 저장 (2단계: 파일 저장)
            this.FormClosing += (s, e) =>
            {
                if (!USE_SERVER)
                    DataManager.SaveToFile(_todos);
            };
        }

        private void PositionHeaderButtons()
        {
            btnTheme.Location = new Point(pnlHeader.Width - btnTheme.Width - 8, 8);
        }

        // ─────────────────────────────────────────────────────────
        //  1단계: 로컬 데이터 로드 (더미 or JSON 파일)
        // ─────────────────────────────────────────────────────────
        private void LoadTodosLocal()
        {
            // 저장된 JSON 파일이 있으면 그걸 쓰고, 없으면 더미 데이터 사용
            _todos = DataManager.LoadOrDummy();

            // DDay 재계산 (날짜 기준으로 오늘과의 차이)
            foreach (var t in _todos)
            {
                if (DateTime.TryParse(t.DueDate, out var due))
                    t.DDay = (int)(due.Date - DateTime.Today).TotalDays;
            }

            lblSummary.Text      = $"📋 총 {_todos.Count}개  |  저장 경로: {DataManager.GetSavePath()}";
            lblSummary.ForeColor = SUBTEXT;

            RebuildTodoList();
            RebuildCalendar();
        }

        // ─────────────────────────────────────────────────────────
        //  2단계 (선택): 서버에서 로드 (USE_SERVER = true 일 때)
        // ─────────────────────────────────────────────────────────
        private async Task LoadTodosFromServerAsync()
        {
            SetLoading(true);
            try
            {
                string json = await _http.GetStringAsync("http://localhost:8000/todos/rule-based");
                var resp = JsonSerializer.Deserialize<TodoResponse>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (resp != null)
                {
                    _todos = resp.Todos;
                    UpdateStats(resp.Statistics);
                    RebuildTodoList();
                    RebuildCalendar();
                }
            }
            catch
            {
                if (InvokeRequired) { Invoke(() => SetLoading(false)); return; }
                lblSummary.Text      = "⚠  서버 연결 실패 — CMD에서 python server.py를 먼저 실행하세요";
                lblSummary.ForeColor = URGENT;
            }
            finally { SetLoading(false); }
        }

        private void UpdateStats(TodoStatistics stats)
        {
            if (InvokeRequired) { Invoke(() => UpdateStats(stats)); return; }
            var parent = lblSummary.Parent;
            if (parent == null) return;
            parent.Controls.Clear();
            var items = new (string t, Color c)[]
            {
                ($"전체 {stats.Total}", TEXT), ($"🔴 긴급 {stats.Urgent}", URGENT),
                ($"🟠 높음 {stats.High}", HIGH_C), ($"📝 시험 {stats.Exams}", ACCENT),
                ($"📋 과제 {stats.Assignments}", Color.FromArgb(55, 175, 115)),
            };
            int x = 2;
            foreach (var (t, c) in items)
            {
                var lbl = new Label { Text = t, ForeColor = c, Font = new Font("맑은 고딕", 8.5f, FontStyle.Bold), AutoSize = true, Location = new Point(x, 8) };
                parent.Controls.Add(lbl);
                x += lbl.PreferredWidth + 18;
            }
        }

        // ─────────────────────────────────────────────────────────
        //  캘린더 재구성
        // ─────────────────────────────────────────────────────────
        private void RebuildCalendar()
        {
            lblMonth.Text = _currentMonth.ToString("yyyy년 MM월");
            if (_selectedDate.HasValue)
                lblSchedDate.Text = $"📅 {_selectedDate.Value:M월 d일} 일정";

            pnlCalendar.Controls.Clear();

            int cellW = 38, cellH = 32;
            string[] dn = { "월", "화", "수", "목", "금", "토", "일" };

            for (int i = 0; i < 7; i++)
                pnlCalendar.Controls.Add(new Label
                {
                    Text      = dn[i], Size = new Size(cellW, 20),
                    Location  = new Point(i * cellW, 0),
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font      = new Font("맑은 고딕", 8.5f, FontStyle.Bold),
                    ForeColor = i >= 5 ? Color.FromArgb(215, 75, 75) : SUBTEXT,
                });

            var dueDates = _todos
                .Where(t => !string.IsNullOrEmpty(t.DueDate))
                .Select(t => { DateTime.TryParse(t.DueDate, out var d); return d.Date; })
                .ToHashSet();

            DateTime first    = new DateTime(_currentMonth.Year, _currentMonth.Month, 1);
            int      startCol = ((int)first.DayOfWeek + 6) % 7;
            int      days     = DateTime.DaysInMonth(_currentMonth.Year, _currentMonth.Month);

            for (int d = 1; d <= days; d++)
            {
                var  date    = new DateTime(_currentMonth.Year, _currentMonth.Month, d);
                int  idx     = startCol + d - 1;
                int  col     = idx % 7;
                int  row     = idx / 7;
                bool isToday = date == DateTime.Today;
                bool isSel   = date == _selectedDate;
                bool hasTodo = dueDates.Contains(date);

                Color bgColor = isToday ? ACCENT : isSel ? SEL_DAY : SURFACE;
                Color fgColor = isToday  ? Color.White
                              : isSel    ? SEL_TEXT
                              : col >= 5 ? Color.FromArgb(215, 75, 75)
                              : TEXT;

                var btn = new Button
                {
                    Text      = d.ToString(),
                    Size      = new Size(cellW - 2, cellH - 2),
                    Location  = new Point(col * cellW, 24 + row * cellH),
                    FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Tag = date,
                    Font      = new Font("맑은 고딕", 8.5f, isToday ? FontStyle.Bold : FontStyle.Regular),
                    BackColor = bgColor, ForeColor = fgColor,
                };
                btn.FlatAppearance.BorderSize = 0;

                if (hasTodo && !isToday)
                    btn.Paint += (s, pe) =>
                    {
                        pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                        using var br = new SolidBrush(URGENT);
                        pe.Graphics.FillEllipse(br, btn.Width / 2 - 3, btn.Height - 7, 6, 6);
                    };

                btn.Click += (s, e) =>
                {
                    _selectedDate = (DateTime)((Button)s!).Tag!;
                    RebuildCalendar();
                    RebuildTodoList();
                    RebuildEventList();
                };
                pnlCalendar.Controls.Add(btn);
            }
            ApplyCalendarTheme();
        }

        // ─────────────────────────────────────────────────────────
        //  TODO 카드 목록
        // ─────────────────────────────────────────────────────────
        private void RebuildTodoList()
        {
            if (InvokeRequired) { Invoke(RebuildTodoList); return; }
            flpTodos.Controls.Clear();

            // 전체 목록을 마감일 순으로 정렬해서 스크롤로 볼 수 있게
            var filtered = _todos
                .OrderBy(t => { DateTime.TryParse(t.DueDate, out var d); return d; })
                .ToList();

            // 헤더: 날짜 선택 여부와 무관하게 전체 표시
            lblTodoTitle.Text = "AI TODO LIST";

            if (!filtered.Any())
            {
                flpTodos.Controls.Add(new Label
                {
                    Text = "할 일이 없습니다 🎉",
                    Font = new Font("맑은 고딕", 10f),
                    ForeColor = SUBTEXT, AutoSize = true, Margin = new Padding(20)
                });
                return;
            }
            foreach (var todo in filtered) flpTodos.Controls.Add(MakeCard(todo));
        }

        private Panel MakeCard(TodoItem todo)
        {
            int cardW = flpTodos.ClientSize.Width - 18;
            if (cardW < 200) cardW = 400;

            var card = new Panel { Width = cardW, Height = 76, BackColor = SURFACE, Margin = new Padding(0, 0, 0, 6), Cursor = Cursors.Hand };
            card.Controls.Add(new Panel { Width = 4, Dock = DockStyle.Left, BackColor = todo.DeadlineColor });

            var btnCheck = new Button { Size = new Size(24, 24), Location = new Point(14, 26), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Tag = todo };
            btnCheck.FlatAppearance.BorderSize = 0;
            btnCheck.FlatAppearance.MouseOverBackColor = Color.Transparent;
            btnCheck.BackColor = Color.Transparent;
            DrawCheckButton(btnCheck, todo);

            var lblIcon  = new Label { Text = todo.TypeIcon, Font = new Font("Segoe UI Emoji", 12f), Location = new Point(44, 10), Size = new Size(26, 26), TextAlign = ContentAlignment.MiddleCenter };
            var lblTitle = new Label { Text = todo.Title + (todo.IsTeamWork ? "  👥" : ""), Font = new Font("맑은 고딕", 9.5f, todo.Completed ? FontStyle.Strikeout : FontStyle.Bold), ForeColor = todo.Completed ? SUBTEXT : TEXT, Location = new Point(74, 8), Size = new Size(cardW - 160, 22), TextAlign = ContentAlignment.MiddleLeft, Tag = "title" };

            // ── 마감 배지: 타원 + 형광펜 투명도 효과 ──────────────
            // 배경색: 해당 색상의 30% 투명도 (형광펜 느낌)
            Color badgeBg   = Color.FromArgb(45, todo.DeadlineColor.R, todo.DeadlineColor.G, todo.DeadlineColor.B);
            Color badgeText = todo.DeadlineColor;  // 글자는 진한 원색

            var lblDeadline = new Label
            {
                Text      = "",            // Paint로 직접 그릴 것
                BackColor = Color.Transparent,
                Size      = new Size(58, 22),
                TextAlign = ContentAlignment.MiddleCenter,
                Anchor    = AnchorStyles.Right | AnchorStyles.Top,
            };
            lblDeadline.Paint += (s, pe) =>
            {
                pe.Graphics.SmoothingMode      = SmoothingMode.AntiAlias;
                pe.Graphics.TextRenderingHint  = System.Drawing.Text.TextRenderingHint.AntiAlias;

                // 타원 배경 (형광펜 투명도)
                using var bgBrush = new SolidBrush(badgeBg);
                pe.Graphics.FillEllipse(bgBrush, 0, 0, lblDeadline.Width - 1, lblDeadline.Height - 1);

                // 텍스트를 정가운데에
                using var sf = new StringFormat
                {
                    Alignment     = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center,
                };
                using var textBrush = new SolidBrush(badgeText);
                pe.Graphics.DrawString(
                    todo.DDayText,
                    new Font("맑은 고딕", 8.5f, FontStyle.Bold),
                    textBrush,
                    new RectangleF(0, 0, lblDeadline.Width, lblDeadline.Height),
                    sf);
            };

            var lblSub = new Label { Text = $"📅 {todo.DueDate}  |  {todo.Reason}", Font = new Font("맑은 고딕", 8.5f), ForeColor = SUBTEXT, Location = new Point(74, 34), Size = new Size(cardW - 160, 18) };
            var lblAct = new Label { Text = todo.ActionItems.Count > 0 ? "✔ " + string.Join("  ·  ", todo.ActionItems.Take(2)) : "", Font = new Font("맑은 고딕", 8f), ForeColor = SUBTEXT, Location = new Point(74, 54), Size = new Size(cardW - 160, 16) };

            card.Controls.AddRange(new Control[] { btnCheck, lblIcon, lblTitle, lblDeadline, lblSub, lblAct });

            btnCheck.Click += (s, e) =>
            {
                todo.Completed = !todo.Completed;
                lblTitle.Font      = new Font("맑은 고딕", 9.5f, todo.Completed ? FontStyle.Strikeout : FontStyle.Bold);
                lblTitle.ForeColor = todo.Completed ? SUBTEXT : TEXT;
                DrawCheckButton(btnCheck, todo);
                // 체크 상태도 즉시 파일에 저장
                if (!USE_SERVER) DataManager.SaveToFile(_todos);
            };

            card.Resize += (s, e) =>
            {
                lblTitle.Width = card.Width - 160;
                lblSub.Width   = card.Width - 160;
                lblAct.Width   = card.Width - 160;
                lblDeadline.Location = new Point(card.Width - 70, 27);
            };
            lblDeadline.Location = new Point(cardW - 70, 27);

            card.MouseEnter += (s, e) => card.BackColor = SURFACE2;
            card.MouseLeave += (s, e) => card.BackColor = SURFACE;
            void open(object? s, EventArgs e) => ShowDetail(todo);
            card.Click += open; lblTitle.Click += open; lblSub.Click += open;
            return card;
        }

        private void DrawCheckButton(Button btn, TodoItem todo)
        {
            btn.Paint -= CheckButtonPaint;
            btn.Paint += CheckButtonPaint;
            btn.Invalidate();
            void CheckButtonPaint(object? s, PaintEventArgs pe)
            {
                pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                var rect = new Rectangle(2, 2, btn.Width - 5, btn.Height - 5);
                if (todo.Completed)
                {
                    using var br  = new SolidBrush(todo.DeadlineColor);
                    pe.Graphics.FillEllipse(br, rect);
                    using var pen = new Pen(Color.White, 2.2f);
                    int cx = rect.Left + rect.Width / 2, cy = rect.Top + rect.Height / 2;
                    pe.Graphics.DrawLine(pen, cx - 5, cy, cx - 2, cy + 4);
                    pe.Graphics.DrawLine(pen, cx - 2, cy + 4, cx + 5, cy - 4);
                }
                else
                {
                    using var pen = new Pen(todo.DeadlineColor, 2f);
                    pe.Graphics.DrawEllipse(pen, rect);
                }
            }
        }

        private void ShowDetail(TodoItem todo)
        {
            var dlg = new Form { Text = todo.Title, Size = new Size(400, 360), StartPosition = FormStartPosition.CenterParent, BackColor = SURFACE, ForeColor = TEXT, Font = new Font("맑은 고딕", 9.5f), FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false };
            var rtb = new RichTextBox { Dock = DockStyle.Fill, BackColor = SURFACE, ForeColor = TEXT, BorderStyle = BorderStyle.None, ReadOnly = true, Font = new Font("맑은 고딕", 10f), Padding = new Padding(14) };
            rtb.SelectionFont = new Font("맑은 고딕", 12f, FontStyle.Bold); rtb.SelectionColor = todo.PriorityColor;
            rtb.AppendText($"{todo.TypeIcon} {todo.Title}\n\n");
            rtb.SelectionFont = new Font("맑은 고딕", 9.5f); rtb.SelectionColor = TEXT;
            rtb.AppendText($"우선순위: {todo.Priority}   {todo.DDayText}\n마감일:   {todo.DueDate}\n유형:     {todo.Type}{(todo.IsTeamWork ? " (팀 과제)" : "")}\n\n");
            rtb.SelectionFont = new Font("맑은 고딕", 9.5f, FontStyle.Bold); rtb.AppendText("💡 판단 이유\n");
            rtb.SelectionFont = new Font("맑은 고딕", 9.5f); rtb.SelectionColor = TEXT; rtb.AppendText($"  {todo.Reason}\n\n");
            if (todo.ActionItems.Any()) { rtb.SelectionFont = new Font("맑은 고딕", 9.5f, FontStyle.Bold); rtb.AppendText("✅ 실행 항목\n"); rtb.SelectionFont = new Font("맑은 고딕", 9.5f); rtb.SelectionColor = TEXT; foreach (var a in todo.ActionItems) rtb.AppendText($"  • {a}\n"); }
            dlg.Controls.Add(rtb); dlg.ShowDialog(this);
        }

        // ─────────────────────────────────────────────────────────
        //  학사일정
        // ─────────────────────────────────────────────────────────
        private void LoadSampleAcademicEvents()
        {
            _events = new List<AcademicEvent>
            {
                new() { Date = new DateTime(2026, 5,  1), Title = "근로자의 날 (공휴일)",  Category = "공휴일", Icon = "🎌" },
                new() { Date = new DateTime(2026, 5,  5), Title = "어린이날 (공휴일)",      Category = "공휴일", Icon = "🎌" },
                new() { Date = new DateTime(2026, 5, 15), Title = "스승의 날",              Category = "행사",   Icon = "👨‍🏫" },
                new() { Date = new DateTime(2026, 5, 20), Title = "중간고사 성적 발표",      Category = "학사",   Icon = "📊" },
                new() { Date = new DateTime(2026, 5, 25), Title = "수강신청 정정 기간 시작", Category = "학사",   Icon = "🏫" },
                new() { Date = new DateTime(2026, 6,  1), Title = "기말고사 기간 시작",      Category = "시험",   Icon = "📝" },
                new() { Date = new DateTime(2026, 6, 10), Title = "기말고사 종료",           Category = "시험",   Icon = "📝" },
                new() { Date = new DateTime(2026, 6, 15), Title = "수강 취소 신청 마감",     Category = "학사",   Icon = "🏫" },
                new() { Date = new DateTime(2026, 6, 20), Title = "성적 입력 마감",          Category = "학사",   Icon = "📋" },
                new() { Date = new DateTime(2026, 6, 25), Title = "성적 열람 기간",          Category = "학사",   Icon = "📊" },
            };
            RebuildEventList();
        }

        private void RebuildEventList()
        {
            if (InvokeRequired) { Invoke(RebuildEventList); return; }
            flpEvents.Controls.Clear();

            var month    = _selectedDate?.Month ?? _currentMonth.Month;
            var year     = _selectedDate?.Year  ?? _currentMonth.Year;
            var filtered = _events.Where(e => e.Date.Year == year && e.Date.Month == month).OrderBy(e => e.Date).ToList();

            lblEvtTitle.Text = $"UNIVERSITY ACADEMIC CALENDAR  ({year}년 {month}월)";

            if (!filtered.Any())
            {
                flpEvents.Controls.Add(new Label { Text = "이 달에 등록된 학사일정이 없습니다.", Font = new Font("맑은 고딕", 10f), ForeColor = SUBTEXT, AutoSize = true, Margin = new Padding(20) });
                return;
            }
            foreach (var ev in filtered) flpEvents.Controls.Add(MakeEventCard(ev));
        }

        private Panel MakeEventCard(AcademicEvent ev)
        {
            int cardW = flpEvents.ClientSize.Width - 18;
            if (cardW < 200) cardW = 400;

            var card = new Panel { Width = cardW, Height = 50, BackColor = SURFACE, Margin = new Padding(0, 0, 0, 2) };

            Color catColor = ev.Category switch
            {
                "시험"   => Color.FromArgb(220, 60,  60),
                "공휴일" => Color.FromArgb(50,  160, 90),
                "학사"   => Color.FromArgb(82,  130, 255),
                _        => Color.FromArgb(130, 140, 155),
            };
            card.Controls.Add(new Panel { Width = 4, Dock = DockStyle.Left, BackColor = catColor });

            var pnlIcon = new Panel { Location = new Point(12, 11), Size = new Size(28, 28), BackColor = Color.Transparent };
            pnlIcon.Paint += (s, pe) =>
            {
                pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Color circleBg = _isDark ? Color.FromArgb(60, 255, 255, 255) : Color.FromArgb(30, catColor.R, catColor.G, catColor.B);
                using var br = new SolidBrush(circleBg);
                pe.Graphics.FillEllipse(br, 0, 0, 27, 27);
            };
            var lblIcon = new Label { Text = ev.Icon, Font = new Font("Segoe UI Emoji", 11f), Location = new Point(0, 0), Size = new Size(28, 28), TextAlign = ContentAlignment.MiddleCenter, BackColor = Color.Transparent };
            pnlIcon.Controls.Add(lblIcon);

            var lblDate = new Label { Text = ev.Date.ToString("M월 d일"), Font = new Font("맑은 고딕", 8.5f, FontStyle.Bold), ForeColor = catColor, Location = new Point(46, 7), Size = new Size(56, 18), TextAlign = ContentAlignment.MiddleLeft };

            var lblCat = new Label { Text = ev.Category, Font = new Font("맑은 고딕", 7f, FontStyle.Bold), ForeColor = Color.White, BackColor = Color.Transparent, Location = new Point(46, 27), Size = new Size(44, 16), TextAlign = ContentAlignment.MiddleCenter };
            lblCat.Paint += (s, pe) =>
            {
                pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var br = new SolidBrush(catColor);
                pe.Graphics.FillRoundedRectangle(br, new Rectangle(0, 0, lblCat.Width - 1, lblCat.Height - 1), 6);
                using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                pe.Graphics.DrawString(ev.Category, lblCat.Font, Brushes.White, new RectangleF(0, 0, lblCat.Width, lblCat.Height), sf);
            };

            var lblTitle = new Label { Text = ev.Title, Font = new Font("맑은 고딕", 9.5f, FontStyle.Bold), ForeColor = TEXT, Location = new Point(96, 15), Size = new Size(cardW - 108, 22), TextAlign = ContentAlignment.MiddleLeft };

            card.Controls.AddRange(new Control[] { pnlIcon, lblDate, lblCat, lblTitle });
            card.Resize += (s, e) => lblTitle.Width = card.Width - 108;
            card.MouseEnter += (s, e) => card.BackColor = SURFACE2;
            card.MouseLeave += (s, e) => card.BackColor = SURFACE;
            return card;
        }

        // ─────────────────────────────────────────────────────────
        //  테마 적용
        // ─────────────────────────────────────────────────────────
        private void ApplyTheme()
        {
            BackColor = BG;
            pnlHeader.BackColor   = SURFACE;
            pnlLeft.BackColor     = SURFACE;
            lblTitle.ForeColor    = TEXT;
            lblDate.ForeColor     = SUBTEXT;
            lblMonth.ForeColor    = TEXT;
            btnPrev.BackColor     = SURFACE2; btnPrev.ForeColor = TEXT;
            btnNext.BackColor     = SURFACE2; btnNext.ForeColor = TEXT;
            btnTheme.BackColor    = SURFACE;  btnTheme.ForeColor = TEXT;

            pnlRight.BackColor    = BG;
            pnlRightTop.BackColor = BG;
            pnlRightBot.BackColor = BG;
            lblTodoTitle.ForeColor = TEXT;
            lblEvtTitle.ForeColor  = TEXT;
            lblSummary.ForeColor   = SUBTEXT;
            lblSchedDate.ForeColor = TEXT;
            lblLoading.ForeColor   = SUBTEXT;
            pnlLoading.BackColor   = BG;
            flpTodos.BackColor     = BG;
            flpEvents.BackColor    = BG;

            btnRefresh.BackColor = ACCENT; btnRefresh.ForeColor = Color.White;

            pnlSplitter.BackColor = _isDark
                ? Color.FromArgb(40, 50, 80)
                : Color.FromArgb(200, 200, 220);

            ApplyCalendarTheme();
            RebuildTodoList();
            RebuildEventList();
        }

        private void ApplyCalendarTheme()
        {
            if (pnlCalendar == null) return;
            pnlCalendar.BackColor = SURFACE;
            foreach (Control c in pnlCalendar.Controls)
            {
                if (c is Label l) l.ForeColor = SUBTEXT;
                if (c is Button b && b.Tag is DateTime date)
                {
                    bool isToday = date == DateTime.Today;
                    bool isSel   = date == _selectedDate;
                    if (!isToday && !isSel) { b.BackColor = SURFACE; b.ForeColor = TEXT; }
                    else if (isSel && !isToday) { b.BackColor = SEL_DAY; b.ForeColor = SEL_TEXT; }
                }
            }
        }

        private void SetLoading(bool on)
        {
            if (InvokeRequired) { Invoke(() => SetLoading(on)); return; }
            pnlLoading.Visible = on; flpTodos.Visible = !on;
            btnRefresh.Enabled = !on;
            btnRefresh.Text    = on ? "⏳ 분석 중..." : "AI 생성";
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _http.Dispose();
            base.Dispose(disposing);
        }
    }

    internal static class GraphicsExtensions
    {
        public static void FillRoundedRectangle(this Graphics g, Brush brush, Rectangle rect, int radius)
        {
            using var path = RoundedRectPath(rect, radius);
            g.FillPath(brush, path);
        }
        private static GraphicsPath RoundedRectPath(Rectangle rect, int r)
        {
            var path = new GraphicsPath();
            path.AddArc(rect.X,             rect.Y,              r * 2, r * 2, 180, 90);
            path.AddArc(rect.Right - r * 2, rect.Y,              r * 2, r * 2, 270, 90);
            path.AddArc(rect.Right - r * 2, rect.Bottom - r * 2, r * 2, r * 2,   0, 90);
            path.AddArc(rect.X,             rect.Bottom - r * 2, r * 2, r * 2,  90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
