using System.Windows;
using System.Windows.Input;
using WinKey   = System.Windows.Input.KeyEventArgs;
using WinInput = System.Windows.Input;

namespace WinKit.Todo
{
    public partial class EditDialog : Window
    {
        public string ResultText { get; private set; } = string.Empty;
        private bool _isUpdatingText = false;

        public EditDialog(string currentText)
        {
            InitializeComponent();
            InputBox.Text = currentText;
            InputBox.SelectAll();
            InputBox.Focus();
        }

        // 标题栏拖动
        private void DlgTitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1) DragMove();
        }

        private void InputBox_PreviewKeyDown(object sender, WinKey e)
        {
            if (e.Key == WinInput.Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) == 0)
            {
                var text = InputBox.Text.Trim();
                if (!string.IsNullOrEmpty(text))
                {
                    ResultText   = text;
                    DialogResult = true;
                }
                e.Handled = true;
            }
            else if (e.Key == WinInput.Key.Escape)
            {
                DialogResult = false;
                e.Handled    = true;
            }
        }

        private void InputBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (_isUpdatingText) return;
            if (sender is System.Windows.Controls.TextBox textBox)
            {
                LimitTextVirtualLength(textBox);
            }
        }

        private void LimitTextVirtualLength(System.Windows.Controls.TextBox textBox)
        {
            var text = textBox.Text;
            int virtualLength = 0;
            int limit = 225; // 15 行 * 15 字
            int truncateIndex = -1;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '\n')
                {
                    virtualLength += 15;
                }
                else if (c == '\r')
                {
                    // 忽略 \r，避免重复
                }
                else
                {
                    virtualLength += 1;
                }

                if (virtualLength > limit)
                {
                    truncateIndex = i;
                    break;
                }
            }

            if (truncateIndex != -1)
            {
                _isUpdatingText = true;
                string truncatedText = text.Substring(0, truncateIndex);
                if (truncatedText.EndsWith("\r"))
                {
                    truncatedText = truncatedText.Substring(0, truncatedText.Length - 1);
                }
                int caret = Math.Min(textBox.CaretIndex, truncatedText.Length);
                textBox.Text = truncatedText;
                textBox.CaretIndex = caret;
                _isUpdatingText = false;
            }
        }
    }
}
