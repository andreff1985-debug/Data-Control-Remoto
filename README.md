# Data Control Remoto

MVP de suporte remoto para Windows, com consentimento explícito do cliente.

## Recursos

- Código temporário de 6 dígitos.
- Dois modos no mesmo programa: receber e prestar suporte.
- Confirmação antes de compartilhar a tela.
- Indicador permanente durante a sessão e encerramento imediato.
- Transmissão protegida por TLS quando o servidor é publicado em HTTPS/WSS.
- Sem instalação silenciosa, acesso não supervisionado ou persistência automática.

## Compilar o executável

Em um computador Windows com o .NET 8 SDK:

```powershell
cd App
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

O executável será criado em:

`App\bin\Release\net8.0-windows\win-x64\publish\DataControlRemoto.exe`

Também é possível enviar o projeto para um repositório GitHub e executar o workflow `build-windows.yml`.

## Servidor seguro

Publique a pasta `RelayServer` em um servidor com .NET 8 e proxy HTTPS (Caddy, Nginx, Azure ou similar):

```bash
cd RelayServer
dotnet run --urls http://127.0.0.1:5080
```

Configure a variável `DATACONTROL_RELAY` no Windows antes de abrir o aplicativo:

```powershell
[Environment]::SetEnvironmentVariable("DATACONTROL_RELAY", "wss://suporte.seudominio.com/ws", "User")
```

Para teste local, use `ws://localhost:5080/ws`.

## Limitações do MVP

- Um monitor por vez.
- Transmissão por imagens JPEG; adequada para suporte, não para vídeo/jogos.
- O servidor deve ser hospedado para conexão pela internet.
- Antes de uso comercial, recomenda-se auditoria de segurança, assinatura digital do `.exe`, política de privacidade e adequação à LGPD.

