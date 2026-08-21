using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using LmuOverlay.Widgets;

namespace LmuOverlay.Desktop;

internal sealed class RemoteDashboardServer : IAsyncDisposable
{
    private static readonly byte[] Page = Encoding.UTF8.GetBytes(Html);
    private readonly int _port;
    private readonly string _token;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly TcpListener _listener;
    private readonly Task _acceptLoop;
    private string _latestJson = "{}";
    private long _sequence;
    private long _lastPublishedAt;

    public RemoteDashboardServer(int port, string token)
    {
        _token = token;
        _listener = new TcpListener(IPAddress.Any, port);
        _listener.Start(8);
        _port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _acceptLoop = Task.Run(AcceptLoopAsync);
    }

    public int Port => _port;
    public string Token => _token;
    public string Url => DisplayUrl(_port, _token);

    public static string DisplayUrl(int port, string token) =>
        $"http://{LocalAddress()}:{port}/?token={Uri.EscapeDataString(token)}";

    public void Publish(DashboardWidgetState state)
    {
        var now = Stopwatch.GetTimestamp();
        if (_lastPublishedAt > 0 &&
            now - _lastPublishedAt < Stopwatch.Frequency / 30)
        {
            return;
        }
        _lastPublishedAt = now;
        var json = JsonSerializer.Serialize(new
        {
            available = state.Available,
            speed = state.SpeedKilometersPerHour,
            gear = state.Gear,
            rpm = state.EngineRpm,
            rpmFraction = state.EngineRpmFraction,
            position = state.Position,
            lap = state.LapNumber,
            track = state.TrackName,
            session = state.SessionName,
            current = state.CurrentLapTimeSeconds,
            last = state.LastLapTimeSeconds,
            best = state.BestLapTimeSeconds,
            optimal = state.OptimalLapTimeSeconds,
            delta = state.DeltaBestSeconds,
            fuel = state.FuelLiters,
            energy = state.VirtualEnergyFraction,
            throttle = state.Throttle,
            brake = state.Brake,
            tcActive = state.TractionControlActive,
            absActive = state.AbsActive,
            tc = state.TractionControlLevel,
            slip = state.TractionControlSlipLevel,
            cut = state.TractionControlCutLevel,
            abs = state.AbsLevel,
            limiter = state.SpeedLimiterActive,
            tires = new[]
            {
                new { name = "FL", temperature = state.TireTemperatures.FrontLeftCelsius, wear = state.TireWear.FrontLeftFraction },
                new { name = "FR", temperature = state.TireTemperatures.FrontRightCelsius, wear = state.TireWear.FrontRightFraction },
                new { name = "RL", temperature = state.TireTemperatures.RearLeftCelsius, wear = state.TireWear.RearLeftFraction },
                new { name = "RR", temperature = state.TireTemperatures.RearRightCelsius, wear = state.TireWear.RearRightFraction },
            },
        });
        Volatile.Write(ref _latestJson, json);
        Interlocked.Increment(ref _sequence);
    }

    private async Task AcceptLoopAsync()
    {
        try
        {
            while (!_shutdown.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(_shutdown.Token)
                    .ConfigureAwait(false);
                _ = Task.Run(() => HandleClientAsync(client, _shutdown.Token));
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken token)
    {
        using (client)
        {
            client.NoDelay = true;
            var stream = client.GetStream();
            var request = await ReadRequestAsync(stream, token).ConfigureAwait(false);
            var firstLine = request.Split("\r\n", 2)[0].Split(' ');
            if (firstLine.Length < 2 || !Authorized(firstLine[1]))
            {
                await WriteStatusAsync(stream, "403 Forbidden", "text/plain", "Forbidden", token);
                return;
            }
            var path = firstLine[1].Split('?', 2)[0];
            if (path.Equals("/events", StringComparison.OrdinalIgnoreCase))
            {
                await StreamEventsAsync(stream, token).ConfigureAwait(false);
                return;
            }
            if (path.Equals("/state", StringComparison.OrdinalIgnoreCase))
            {
                await WriteStatusAsync(
                    stream,
                    "200 OK",
                    "application/json; charset=utf-8",
                    Volatile.Read(ref _latestJson),
                    token).ConfigureAwait(false);
                return;
            }
            await WriteBytesAsync(stream, "200 OK", "text/html; charset=utf-8", Page, token);
        }
    }

    private bool Authorized(string target)
    {
        var query = target.Split('?', 2).ElementAtOrDefault(1) ?? string.Empty;
        foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = part.Split('=', 2);
            if (pair.Length == 2 && pair[0] == "token" &&
                string.Equals(Uri.UnescapeDataString(pair[1]), _token, StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    private async Task StreamEventsAsync(NetworkStream stream, CancellationToken token)
    {
        var header = Encoding.ASCII.GetBytes(
            "HTTP/1.1 200 OK\r\nContent-Type: text/event-stream\r\n" +
            "Cache-Control: no-cache\r\nConnection: keep-alive\r\n" +
            "X-Accel-Buffering: no\r\n\r\n");
        await stream.WriteAsync(header, token).ConfigureAwait(false);
        long sent = -1;
        while (!token.IsCancellationRequested && stream.CanWrite)
        {
            var sequence = Interlocked.Read(ref _sequence);
            if (sequence != sent)
            {
                var payload = Encoding.UTF8.GetBytes(
                    $"id: {sequence}\ndata: {Volatile.Read(ref _latestJson)}\n\n");
                await stream.WriteAsync(payload, token).ConfigureAwait(false);
                await stream.FlushAsync(token).ConfigureAwait(false);
                sent = sequence;
            }
            await Task.Delay(16, token).ConfigureAwait(false);
        }
    }

    private static async Task<string> ReadRequestAsync(NetworkStream stream, CancellationToken token)
    {
        var bytes = new List<byte>(2048);
        var one = new byte[1];
        while (bytes.Count < 16_384)
        {
            if (await stream.ReadAsync(one, token).ConfigureAwait(false) == 0) break;
            bytes.Add(one[0]);
            var count = bytes.Count;
            if (count >= 4 && bytes[count - 4] == 13 && bytes[count - 3] == 10 &&
                bytes[count - 2] == 13 && bytes[count - 1] == 10) break;
        }
        return Encoding.ASCII.GetString(bytes.ToArray());
    }

    private static Task WriteStatusAsync(
        NetworkStream stream, string status, string type, string value, CancellationToken token) =>
        WriteBytesAsync(stream, status, type, Encoding.UTF8.GetBytes(value), token);

    private static async Task WriteBytesAsync(
        NetworkStream stream, string status, string type, byte[] body, CancellationToken token)
    {
        var header = Encoding.ASCII.GetBytes(
            $"HTTP/1.1 {status}\r\nContent-Type: {type}\r\n" +
            $"Content-Length: {body.Length}\r\nCache-Control: no-store\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(header, token).ConfigureAwait(false);
        await stream.WriteAsync(body, token).ConfigureAwait(false);
    }

    private static string LocalAddress()
    {
        try
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(item => item.OperationalStatus == OperationalStatus.Up &&
                               item.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .SelectMany(item => item.GetIPProperties().UnicastAddresses)
                .Select(item => item.Address)
                .FirstOrDefault(address => address.AddressFamily == AddressFamily.InterNetwork &&
                                           !IPAddress.IsLoopback(address))?.ToString()
                ?? "127.0.0.1";
        }
        catch
        {
            return "127.0.0.1";
        }
    }

    public async ValueTask DisposeAsync()
    {
        _shutdown.Cancel();
        _listener.Stop();
        try { await _acceptLoop.ConfigureAwait(false); } catch { }
        _shutdown.Dispose();
    }

    private const string Html = """
<!doctype html><html><head><meta name="viewport" content="width=device-width,initial-scale=1,viewport-fit=cover">
<title>RedFox Racing Dashboard</title><style>
:root{color-scheme:dark;font-family:Bahnschrift,Arial,sans-serif}*{box-sizing:border-box}body{margin:0;background:#05080a;color:#f5f7fa;overflow:hidden}.dash{height:100dvh;padding:1.4vmin;display:grid;grid-template:10% 42% 44%/29% 40% 29%;gap:1%}.panel{border:1px solid #33414d;background:#090d10;border-radius:1.3vmin;padding:1.4vmin}.head{grid-column:1/4;display:flex;align-items:center;justify-content:space-between;border:2px solid #42d3a6}.title{font-size:3.7vmin;font-weight:800}.muted{color:#99a6b4}.left{grid-row:2}.center{grid-row:2;text-align:center}.right{grid-row:2}.gear{font-size:16vmin;font-weight:800;line-height:.9}.speed{font-size:4.5vmin;color:#cad3dc}.rpm{font-size:2.6vmin}.lights{display:flex;gap:.6vmin;justify-content:center;margin-bottom:1vmin}.light{width:1.8vmin;height:1.8vmin;border-radius:50%;background:#26313b}.modules{grid-column:1/4;display:grid;grid-template-columns:1fr 1fr;gap:1%}.tires{display:grid;grid-template-columns:1fr 1fr;gap:1vmin}.tire{padding:1.2vmin;border-left:1.1vmin solid #42d3a6;font-size:2.5vmin}.telemetry{position:relative}canvas{width:100%;height:75%;background:#070a0c;border:1px solid #26313b}.row{display:flex;justify-content:space-between;font-size:2.35vmin;margin:.8vmin 0}.big{font-size:3.1vmin;font-weight:700}.green{color:#42d3a6}.cyan{color:#12d9e5}.amber{color:#ffbe40}.red{color:#ff464b}@media(max-aspect-ratio:1/1){.dash{grid-template:9% 34% 54%/30% 38% 30%}.modules{grid-template-columns:1fr}.telemetry{display:none}.title{font-size:4vw}.row{font-size:2.5vw}}
</style></head><body><main class="dash"><section class="panel head"><span id="track">LMU</span><span class="title">REDFOX RACING</span><span id="status" class="green">CONECTANDO</span></section><section class="panel left"><div class="row big green"><span>POS</span><span id="pos">--</span></div><div class="row"><span>LAP</span><span id="lap">--</span></div><div class="row amber"><span>DELTA</span><span id="delta">--</span></div><div class="row"><span>FUEL</span><span id="fuel">--</span></div><div class="row cyan"><span>NRG</span><span id="energy">--</span></div></section><section class="panel center"><div id="lights" class="lights"></div><div id="speed" class="speed">--- KM/H</div><div id="gear" class="gear">N</div><div id="rpm" class="rpm">RPM ----</div><div id="limiter" class="amber"></div></section><section class="panel right"><div class="row"><span>CURRENT</span><span id="current">--:--.---</span></div><div class="row muted"><span>LAST</span><span id="last">--:--.---</span></div><div class="row green"><span>BEST</span><span id="best">--:--.---</span></div><div class="row cyan"><span>OPTIMAL</span><span id="optimal">--:--.---</span></div><div class="row"><span>TC / SLIP / CUT / ABS</span><span id="aids">-- / -- / -- / --</span></div></section><section class="modules"><div class="panel"><div class="amber">TYRE TEMP / WEAR</div><div id="tires" class="tires"></div></div><div class="panel telemetry"><div class="cyan">TELEMETRY</div><canvas id="graph"></canvas><div class="row"><span id="thr" class="green">THR --</span><span id="brk" class="red">BRK --</span></div></div></section></main><script>
var byId=function(id){return document.getElementById(id);};
var lights=byId('lights'),historyPoints=[],canvas=byId('graph'),context=canvas.getContext('2d');
for(var lightIndex=0;lightIndex<12;lightIndex++){var light=document.createElement('i');light.className='light';lights.appendChild(light);}
function valueOr(value,fallback){return value===null||typeof value==='undefined'?fallback:value;}
function formatTime(value){if(!value||value<=0)return'--:--.---';var minutes=Math.floor(value/60),seconds=value-minutes*60;var text=seconds.toFixed(3);while(text.length<6)text='0'+text;return minutes+':'+text;}
function drawGraph(){var ratio=window.devicePixelRatio||1;canvas.width=canvas.clientWidth*ratio;canvas.height=canvas.clientHeight*ratio;var width=canvas.width,height=canvas.height;context.clearRect(0,0,width,height);context.strokeStyle='#26313b';for(var grid=1;grid<4;grid++){context.beginPath();context.moveTo(0,height*grid/4);context.lineTo(width,height*grid/4);context.stroke();}var series=[['t','#42d3a6'],['b','#ff464b']];for(var seriesIndex=0;seriesIndex<series.length;seriesIndex++){var key=series[seriesIndex][0];context.strokeStyle=series[seriesIndex][1];context.lineWidth=2*ratio;context.beginPath();for(var pointIndex=0;pointIndex<historyPoints.length;pointIndex++){var x=pointIndex/Math.max(1,historyPoints.length-1)*width;var y=height-historyPoints[pointIndex][key]*height;if(pointIndex)context.lineTo(x,y);else context.moveTo(x,y);}context.stroke();}}
function update(data){byId('status').textContent=data.available?'CONECTADO':'AGUARDANDO';byId('track').textContent=data.track||'LMU';byId('pos').textContent=data.position||'--';byId('lap').textContent=data.lap||'--';byId('delta').textContent=Number(valueOr(data.delta,0)).toFixed(3);byId('fuel').textContent=Number(valueOr(data.fuel,0)).toFixed(1)+' L';byId('energy').textContent=Math.round(valueOr(data.energy,0)*100)+'%';byId('speed').textContent=Math.round(data.speed||0)+' KM/H';byId('gear').textContent=data.gear||'N';byId('rpm').textContent='RPM '+Math.round(data.rpm||0);byId('limiter').textContent=data.limiter?'PIT LIMITER':'';byId('current').textContent=formatTime(data.current);byId('last').textContent=formatTime(data.last);byId('best').textContent=formatTime(data.best);byId('optimal').textContent=formatTime(data.optimal);byId('aids').textContent=valueOr(data.tc,'--')+' / '+valueOr(data.slip,'--')+' / '+valueOr(data.cut,'--')+' / '+valueOr(data.abs,'--');var active=Math.ceil(Math.max(0,Math.min(1,data.rpmFraction||0))*12);for(var index=0;index<lights.children.length;index++){lights.children[index].style.background=index<active?(index<4?'#42da6d':index<7?'#ffbe40':index<10?'#ff464b':'#4178ff'):'#26313b';}var tireHtml='',tires=data.tires||[];for(var tireIndex=0;tireIndex<tires.length;tireIndex++){var tire=tires[tireIndex];tireHtml+='<div class="tire"><b>'+tire.name+'</b> '+Math.round(tire.temperature)+'° · '+Math.round(tire.wear*100)+'%</div>';}byId('tires').innerHTML=tireHtml;byId('thr').textContent='THR '+Math.round((data.throttle||0)*100)+'%';byId('brk').textContent='BRK '+Math.round((data.brake||0)*100)+'%';historyPoints.push({t:data.throttle||0,b:data.brake||0});if(historyPoints.length>120)historyPoints.shift();drawGraph();}
var query=window.location.search||'',polling=false,streamOpened=false,lastFrameAt=0,eventStream=null;
function startPolling(){if(polling)return;polling=true;if(eventStream){eventStream.close();eventStream=null;}function poll(){var request=new XMLHttpRequest(),scheduled=false;function next(delay){if(scheduled)return;scheduled=true;window.setTimeout(poll,delay);}request.open('GET','/state'+query+'&cache='+new Date().getTime(),true);request.timeout=2000;request.onreadystatechange=function(){if(request.readyState!==4)return;if(request.status===200){try{update(JSON.parse(request.responseText));lastFrameAt=new Date().getTime();}catch(error){byId('status').textContent='DADOS INVÁLIDOS';}}else{byId('status').textContent='ERRO '+request.status;}next(50);};request.ontimeout=request.onerror=function(){byId('status').textContent='RECONECTANDO';next(250);};request.send(null);}poll();}
byId('status').textContent='INICIANDO';
if(window.EventSource){eventStream=new EventSource('/events'+query);eventStream.onopen=function(){streamOpened=true;byId('status').textContent='CONECTADO';};eventStream.onmessage=function(event){try{update(JSON.parse(event.data));lastFrameAt=new Date().getTime();}catch(error){startPolling();}};eventStream.onerror=function(){startPolling();};window.setTimeout(function(){if(!streamOpened||!lastFrameAt)startPolling();},1500);}else{startPolling();}
</script></body></html>
""";
}
