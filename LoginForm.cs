namespace KwuTodoAI
{
    public class LoginForm : Form
    {
        private bool    _isDark = false;
        private Panel   pnlCard  = null!;
        private Label   lblLogo  = null!, lblSub = null!, lblId = null!, lblPw = null!, lblErr = null!;
        private TextBox txtId    = null!, txtPw  = null!;
        private Button  btnLogin = null!, btnTheme = null!;

        private Color BG      => _isDark ? Color.FromArgb(18, 22, 36)   : Color.FromArgb(235, 240, 255);
        private Color CARD    => _isDark ? Color.FromArgb(28, 33, 52)   : Color.White;
        private Color ACCENT  => Color.FromArgb(82, 130, 255);
        private Color TEXT    => _isDark ? Color.FromArgb(220, 228, 255): Color.FromArgb(30, 35, 60);
        private Color SUBTEXT => _isDark ? Color.FromArgb(130, 145, 180): Color.FromArgb(110, 120, 155);
        private Color INPUTBG => _isDark ? Color.FromArgb(38, 44, 68)   : Color.FromArgb(248, 250, 255);

        public LoginForm()
        {
            Text            = "광운대 AI TODO";
            Size            = new Size(420, 520);
            StartPosition   = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox     = false;
            DoubleBuffered  = true;
            Font            = new Font("맑은 고딕", 9.5f);
            BuildUI();
            ApplyTheme();
        }

        private void BuildUI()
        {
            btnTheme = new Button
            {
                Text      = "🌙",
                Size      = new Size(36, 36),
                FlatStyle = FlatStyle.Flat,
                Cursor    = Cursors.Hand,
                Font      = new Font("Segoe UI Emoji", 14f),
                Anchor    = AnchorStyles.Top | AnchorStyles.Right,
                Location  = new Point(346, 10),
            };
            btnTheme.FlatAppearance.BorderSize = 0;
            btnTheme.Click += (s, e) => { _isDark = !_isDark; btnTheme.Text = _isDark ? "☀️" : "🌙"; ApplyTheme(); };

            pnlCard = new Panel { Size = new Size(320, 390), Location = new Point(50, 60) };

            lblLogo = new Label { Text = "🎓 KWU TODO", Font = new Font("맑은 고딕", 20f, FontStyle.Bold), AutoSize = true, Location = new Point(28, 30) };
            lblSub  = new Label { Text = "광운대 AI 학사일정 관리 시스템", Font = new Font("맑은 고딕", 9f), AutoSize = true, Location = new Point(32, 68) };
            lblId   = new Label { Text = "학번", Font = new Font("맑은 고딕", 9f, FontStyle.Bold), AutoSize = true, Location = new Point(28, 114) };

            txtId = new TextBox
            {
                PlaceholderText = "학번을 입력하세요",
                Font            = new Font("맑은 고딕", 10f),
                Size            = new Size(264, 34),
                Location        = new Point(28, 136),
                BorderStyle     = BorderStyle.FixedSingle,
            };
            txtId.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) txtPw.Focus(); };

            lblPw = new Label { Text = "비밀번호", Font = new Font("맑은 고딕", 9f, FontStyle.Bold), AutoSize = true, Location = new Point(28, 186) };

            txtPw = new TextBox
            {
                PlaceholderText = "비밀번호를 입력하세요",
                Font            = new Font("맑은 고딕", 10f),
                Size            = new Size(264, 34),
                Location        = new Point(28, 208),
                BorderStyle     = BorderStyle.FixedSingle,
                PasswordChar    = '●',
            };
            txtPw.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) DoLogin(); };

            lblErr = new Label { Text = "", Font = new Font("맑은 고딕", 8.5f), ForeColor = Color.FromArgb(220, 60, 60), AutoSize = true, Location = new Point(28, 252) };

            btnLogin = new Button
            {
                Text      = "로그인",
                Font      = new Font("맑은 고딕", 11f, FontStyle.Bold),
                Size      = new Size(264, 46),
                Location  = new Point(28, 278),
                FlatStyle = FlatStyle.Flat,
                BackColor = ACCENT,
                ForeColor = Color.White,
                Cursor    = Cursors.Hand,
            };
            btnLogin.FlatAppearance.BorderSize = 0;
            btnLogin.Click += (s, e) => DoLogin();

            pnlCard.Controls.AddRange(new Control[] { lblLogo, lblSub, lblId, txtId, lblPw, txtPw, lblErr, btnLogin });
            Controls.AddRange(new Control[] { pnlCard, btnTheme });
        }

        private void DoLogin()
        {
            if (string.IsNullOrWhiteSpace(txtId.Text)) { lblErr.Text = "⚠ 학번을 입력해주세요."; return; }
            if (string.IsNullOrWhiteSpace(txtPw.Text)) { lblErr.Text = "⚠ 비밀번호를 입력해주세요."; return; }
            var main = new MainForm(_isDark);
            main.Show();
            Hide();
            main.FormClosed += (s, e) => Close();
        }

        private void ApplyTheme()
        {
            BackColor          = BG;
            pnlCard.BackColor  = CARD;
            lblLogo.ForeColor  = ACCENT;
            lblSub.ForeColor   = SUBTEXT;
            lblId.ForeColor    = TEXT;
            lblPw.ForeColor    = TEXT;
            txtId.BackColor    = INPUTBG;
            txtId.ForeColor    = TEXT;
            txtPw.BackColor    = INPUTBG;
            txtPw.ForeColor    = TEXT;
            btnTheme.BackColor = BG;
            btnTheme.ForeColor = TEXT;
            Invalidate();
        }
    }
}
