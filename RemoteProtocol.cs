using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace DataControlRemoto;

internal sealed record InputEvent(string Kind, double X = 0, double Y = 0, int Button = 0, int Key = 0);

internal static class RemoteProtocol
{
    public static async Task SendText(ClientWebSocket socket, string text, CancellationToken ct) =>
        await socket.SendAsync(Encoding.UTF8.GetBytes(text), WebSocketMessageType.Text, true, ct);

    public static async Task SendBinary(ClientWebSocket socket, byte[] bytes, CancellationToken ct) =>
        await socket.SendAsync(bytes, WebSocketMessageType.Binary, true, ct);

    public static string SerializeInput(InputEvent e) => "input:" + JsonSerializer.Serialize(e);

    public static InputEvent? ParseInput(string text) =>
        text.StartsWith("input:") ? JsonSerializer.Deserialize<InputEvent>(text[6..]) : null;
}
