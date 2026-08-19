using System.Drawing.Imaging;
using System.Net.WebSockets;
using System.Text;

namespace DataControlRemoto;

internal sealed class MainForm : Form
{
    readonly string relay = Environment.GetEnvironmentVariable("DATACONTROL_RELAY") ?? "ws://localhost:5080/ws";
    readonly Label title = new() { Text = "DATA CONTROL REMOTO", AutoSize = true, Font = new("Segoe UI", 20, FontStyle.Bold), ForeColor = Color.FromArgb(25, 88, 165), Location = new(24, 20) };
    readonly Label status = new() { Text = "Pronto para conectar", AutoSize = true, Location = new(27, 68), ForeColor = Color.DimGray };
    readonly TextBox codeBox = new() { Font = new("Consolas", 22, FontStyle.Bold), TextAlign = HorizontalAlignment.Center, MaxLength = 6, Location = new(27, 117), Width = 220 };
    readonly Button receiveButton = new() { Text = "RECEBER SUPORTE", Location = new(27, 178), Width = 220, Height = 44 };
    readonly Button supportButton = new() { Text = "PRESTAR SUPORTE", Location = new(267, 117), Width = 220, Height = 44 };
    readonly Button stopButton = new() { Text = "ENCERRAR SESSÃO", Location = new(267, 178), Width = 220, Height = 44, BackColor = Color.Firebrick, ForeColor = Color.White, Enabled = false };
    readonly PictureBox viewer = new() { Location = new(15, 250), Size = new(900, 505), SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.FromArgb(30, 30, 30), Visible = false, TabStop = true };
    ClientWebSocket? socket;
    CancellationTokenSource? sessionCts;
    bool authorized;

    public MainForm()
    {
        Text = "Data Control Remoto"; ClientSize = new(930, 770); MinimumSize = new(540, 310); StartPosition = FormStartPosition.CenterScreen;
        Controls.AddRange([title, status, codeBox, receiveButton, supportButton, stopButton, viewer]);
        receiveButton.Click += async (_, _) => await ReceiveSupport();
        supportButton.Click += async (_, _) => await ProvideSupport();
        stopButton.Click += async (_, _) => await StopSession();
        viewer.MouseMove += async (_, e) => await SendMouse(e, 0);
        viewer.MouseDown += async (_, e) => await SendMouse(e, e.Button == MouseButtons.Left ? 1 : 3);
        viewer.MouseUp += async (_, e) => await SendMouse(e, e.Button == MouseButtons.Left ? 2 : 4);
        viewer.KeyDown += async (_, e) => { await SendInput(new("keyDown", Key: e.KeyValue)); e.SuppressKeyPress = true; };
        viewer.KeyUp += async (_, e) => { await SendInput(new("keyUp", Key: e.KeyValue)); e.SuppressKeyPress = true; };
        FormClosing += async (_, _) => await StopSession();
    }

    async Task ReceiveSupport()
    {
        var code = Random.Shared.Next(100000, 999999).ToString(); codeBox.Text = code;
        await Connect("host", code); status.Text = "Aguardando técnico. Informe somente este código.";
    }

    async Task ProvideSupport()
    {
        if (codeBox.Text.Length != 6 || !codeBox.Text.All(char.IsDigit)) { MessageBox.Show("Digite o código de 6 dígitos fornecido pelo cliente."); return; }
        await Connect("controller", codeBox.Text); viewer.Visible = true; viewer.Focus(); status.Text = "Solicitação enviada. Aguardando autorização do cliente.";
    }

    async Task Connect(string role, string code)
    {
        await StopSession(); sessionCts = new(); socket = new();
        try
        {
            await socket.ConnectAsync(new Uri($"{relay}?role={role}&code={code}"), sessionCts.Token);
            stopButton.Enabled = true; receiveButton.Enabled = supportButton.Enabled = false;
            _ = ReadLoop(role, sessionCts.Token);
        }
        catch (Exception ex) { MessageBox.Show("Não foi possível conectar ao servidor.\n\n" + ex.Message); await StopSession(); }
    }

    async Task ReadLoop(string role, CancellationToken ct)
    {
        var buffer = new byte[4 * 1024 * 1024];
        try
        {
            while (socket?.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                using var ms = new MemoryStream(); WebSocketReceiveResult result;
                do { result = await socket.ReceiveAsync(buffer, ct); ms.Write(buffer, 0, result.Count); } while (!result.EndOfMessage);
                if (result.MessageType == WebSocketMessageType.Close) break;
                if (result.MessageType == WebSocketMessageType.Binary && role == "controller")
                {
                    ms.Position = 0; using var img = Image.FromStream(ms); var copy = new Bitmap(img);
                    BeginInvoke(() => { var old = viewer.Image; viewer.Image = copy; old?.Dispose(); status.Text = "Sessão ativa — o cliente pode encerrar a qualquer momento."; });
                }
                else if (result.MessageType == WebSocketMessageType.Text)
                {
                    var text = Encoding.UTF8.GetString(ms.ToArray());
                    if (role == "host") await HandleHostMessage(text, ct);
                }
            }
        }
        catch when (ct.IsCancellationRequested) { }
        catch (Exception ex) { BeginInvoke(() => status.Text = "Sessão interrompida: " + ex.Message); }
        finally { BeginInvoke(async () => await StopSession()); }
    }

    async Task HandleHostMessage(string text, CancellationToken ct)
    {
        if (text == "peer-connected" && !authorized)
        {
            var answer = MessageBox.Show("Um técnico deseja visualizar e controlar este computador.\n\nAutorizar somente se você solicitou o atendimento. Deseja permitir?", "Autorização de suporte remoto", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (answer == DialogResult.Yes) { authorized = true; BeginInvoke(() => status.Text = "SUPORTE ATIVO — clique em Encerrar Sessão para interromper"); await RemoteProtocol.SendText(socket!, "authorized", ct); _ = CaptureLoop(ct); }
            else await RemoteProtocol.SendText(socket!, "denied", ct);
        }
        else if (authorized)
        {
            var input = RemoteProtocol.ParseInput(text);
            if (input?.Kind == "mouse") NativeInput.Mouse(input.X, input.Y, input.Button);
            else if (input?.Kind == "keyDown") NativeInput.Key(input.Key, true);
            else if (input?.Kind == "keyUp") NativeInput.Key(input.Key, false);
        }
    }

    async Task CaptureLoop(CancellationToken ct)
    {
        while (authorized && socket?.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            var bounds = Screen.PrimaryScreen!.Bounds;
            using var bmp = new Bitmap(bounds.Width, bounds.Height); using (var g = Graphics.FromImage(bmp)) g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
            using var scaled = new Bitmap(bmp, Math.Min(1280, bounds.Width), Math.Min(720, bounds.Height)); using var ms = new MemoryStream();
            var codec = ImageCodecInfo.GetImageEncoders().First(x => x.MimeType == "image/jpeg"); using var ep = new EncoderParameters(1); ep.Param[0] = new EncoderParameter(Encoder.Quality, 55L); scaled.Save(ms, codec, ep);
            await RemoteProtocol.SendBinary(socket, ms.ToArray(), ct); await Task.Delay(120, ct);
        }
    }

    async Task SendMouse(MouseEventArgs e, int action) => await SendInput(new("mouse", (double)e.X / viewer.Width, (double)e.Y / viewer.Height, action));
    async Task SendInput(InputEvent input) { if (socket?.State == WebSocketState.Open) await RemoteProtocol.SendText(socket, RemoteProtocol.SerializeInput(input), sessionCts!.Token); }

    async Task StopSession()
    {
        authorized = false; sessionCts?.Cancel();
        if (socket?.State == WebSocketState.Open) try { await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Sessão encerrada", CancellationToken.None); } catch { }
        socket?.Dispose(); socket = null; sessionCts?.Dispose(); sessionCts = null;
        if (!IsDisposed) { stopButton.Enabled = false; receiveButton.Enabled = supportButton.Enabled = true; viewer.Visible = false; viewer.Image?.Dispose(); viewer.Image = null; status.Text = "Pronto para conectar"; }
    }
}
