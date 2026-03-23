#nullable enable
namespace AIRoundTable
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer? components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                components?.Dispose();
                // 인스턴스 폰트 해제 (개선 #3: 리소스 누수 방지)
                _fontSender?.Dispose();
                _fontMsg?.Dispose();
                _fontTime?.Dispose();
            }
            base.Dispose(disposing);
        }

        // BuildUI()에서 모든 컨트롤을 직접 생성하므로
        // 디자이너 스텁은 빈 상태로 유지합니다.
        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.ResumeLayout(false);
        }
    }
}
