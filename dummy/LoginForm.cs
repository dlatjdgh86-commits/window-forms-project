namespace KwuTodoAI
{
    public partial class LoginForm : Form
    {
        private bool _isDark = false;

        private Color BG      => _isDark ? Color.FromArgb(18, 22, 36)    : Color.FromArgb(235, 240, 255);
        private Color CARD    => _isDark ? Color.FromArgb(28, 33, 52)    : Color.White;
        private Color ACCENT  => Color.FromArgb(82, 130, 255);
        private Color TEXT    => _isDark ? Color.FromArgb(220, 228, 255) : Color.FromArgb(30, 35, 60);
        private Color SUBTEXT => _isDark ? Color.FromArgb(130, 145, 180) : Color.FromArgb(110, 120, 155);
        private Color INPUTBG => _isDark ? Color.FromArgb(38, 44, 68)    : Color.FromArgb(248, 250, 255);

        public LoginForm()
        {
            InitializeComponent();
            HookEvents();
            ApplyTheme();
        }

        private void HookEvents()
        {
            btnTheme.Click  += (s, e) => { _isDark = !_isDark; btnTheme.Text = _isDark ? "☀️" : "🌙"; ApplyTheme(); };
            btnLogin.Click  += (s, e) => DoLogin();
            txtId.KeyDown   += (s, e) => { if (e.KeyCode == Keys.Enter) txtPw.Focus(); };
            txtPw.KeyDown   += (s, e) => { if (e.KeyCode == Keys.Enter) DoLogin(); };

            // 카드 좌우 여백 균등
            this.Resize += (s, e) => CenterCard();
            this.Load   += (s, e) => CenterCard();
        }

        private void CenterCard()
        {
            int x = (ClientSize.Width  - pnlCard.Width)  / 2;
            int y = (ClientSize.Height - pnlCard.Height) / 2 - 10;
            pnlCard.Location = new Point(Math.Max(0, x), Math.Max(50, y));
        }

        private void DoLogin()
        {
            lblErr.Text = "";
            if (string.IsNullOrWhiteSpace(txtId.Text)) { lblErr.Text = "⚠ 학번을 입력해주세요."; return; }
            if (string.IsNullOrWhiteSpace(txtPw.Text)) { lblErr.Text = "⚠ 비밀번호를 입력해주세요."; return; }
            var main = new MainForm(_isDark);
            main.Show();
            Hide();
            main.FormClosed += (s, e) => Close();
        }

        private void ApplyTheme()
        {
            BackColor         = BG;
            pnlCard.BackColor = CARD;
            lblLogo.ForeColor = ACCENT;
            lblSub.ForeColor  = SUBTEXT;
            lblId.ForeColor   = TEXT;
            lblPw.ForeColor   = TEXT;
            txtId.BackColor   = INPUTBG;
            txtId.ForeColor   = TEXT;
            txtPw.BackColor   = INPUTBG;
            txtPw.ForeColor   = TEXT;
            btnTheme.BackColor = BG;
            btnTheme.ForeColor = TEXT;
            Invalidate();
        }
    }
}
