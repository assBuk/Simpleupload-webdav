using GetSpaceWebDav.Properties;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GetSpaceWebDav
{
    public class MainForm : Form
    {
        private NotifyIcon notifyIcon;

        public MainForm()
        {
            this.Load += MainForm_Load;
        }
        private void MainForm_Load(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Minimized;
        }
    }

    internal class ProgressableStreamContent : HttpContent
    {
        private readonly Stream _content;
        private readonly Action<long> _progress;

        public ProgressableStreamContent(Stream content, Action<long> progress)
        {
            _content = content;
            _progress = progress;
        }

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext context)
        {
            var buffer = new byte[8192];
            int bytesRead;
            long totalBytesRead = 0;

            while ((bytesRead = await _content.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await stream.WriteAsync(buffer, 0, bytesRead);
                totalBytesRead += bytesRead;
                _progress(totalBytesRead);
            }
        }

        protected override bool TryComputeLength(out long length)
        {
            length = _content.Length;
            return true;
        }
    }

    internal class GetSpaceWebDav
    {
        [STAThread]
        static void Main(string[] args)
        {
            bool startMinimized = args.Contains("/StartMinimized");

            if (startMinimized)
            {
                MainForm mainForm = new MainForm();
                Application.DoEvents();
                Application.Run(mainForm);
                return;
            }

            if (args.Length == 0)
            {
                MessageBox.Show("Укажите путь к файлу для загрузки.", "Информация", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var filePath = args[0];
            if (!File.Exists(filePath))
            {
                MessageBox.Show("Файл не найден.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var settingsFilePath = Path.Combine(appDirectory, "Setting.txt");

            if (!File.Exists(settingsFilePath))
            {
                File.WriteAllText(settingsFilePath, "WEBDAV_URL=\nUSERNAME=\nPASSWORD=\n");
            }

            var settings = File.ReadAllLines(settingsFilePath)
                .Select(line => line.Split('='))
                .ToDictionary(parts => parts[0], parts => parts[1]);

            var webDavUrl = !string.IsNullOrWhiteSpace(settings["WEBDAV_URL"]) ? settings["WEBDAV_URL"] : Resources.WEBDAV_URL;
            var username = !string.IsNullOrWhiteSpace(settings["USERNAME"]) ? settings["USERNAME"] : Resources.USERNAME;
            var password = !string.IsNullOrWhiteSpace(settings["PASSWORD"]) ? settings["PASSWORD"] : Resources.PASSWORD;

            string exePath = Process.GetCurrentProcess().MainModule.FileName;
            Icon icon = Icon.ExtractAssociatedIcon(exePath);
            NotifyIcon notifyIcon = new NotifyIcon
            {
                Icon = icon,
                Visible = true,
                Text = "Getspace uploader WEBDAV"
            };

            try
            {
                using (var client = new HttpClient())
                {
                    var byteArray = Encoding.ASCII.GetBytes($"{username}:{password}");
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));

                    using (var fileStream = File.OpenRead(filePath))
                    {
                        // Вызов уведомления о начале загрузки
                        notifyIcon.ShowBalloonTip(3000, "Загрузка началась", "Процесс загрузки файла начался.", ToolTipIcon.Info);

                        var content = new ProgressableStreamContent(fileStream, bytesSent => { });
                        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                        var response = client.PutAsync($"{webDavUrl}/{Path.GetFileName(filePath)}", content).Result;

                        if (response.IsSuccessStatusCode)
                        {
                            //MessageBox.Show($"Файл успешно загружен: {filePath}", "Успех", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            notifyIcon.ShowBalloonTip(3000, "Успех", $"Файл успешно загружен\n{filePath}", ToolTipIcon.Info);
                        }
                        else
                        {
                            notifyIcon.ShowBalloonTip(3000, "Ошибка", "Ошибка при загрузке файла.", ToolTipIcon.Error);
                            MessageBox.Show($"Ошибка при загрузке файла: {response.StatusCode}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке файла: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
