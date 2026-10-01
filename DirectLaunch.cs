using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

public class LaunchException : Exception { public LaunchException(string message):base(message) {} }
public static class DirectLaunch {
    public static async Task<string> GetTicket(Account account,ulong placeId,bool follow=false) {
        if (String.IsNullOrEmpty(account.Token)) throw new LaunchException("Sign in again with Open Account to renew the saved session.");
        var cookies=new CookieContainer();
        cookies.Add(new Uri("https://auth.roblox.com"),new Cookie(".ROBLOSECURITY",account.Token,"/",".roblox.com") { Secure=true,HttpOnly=true });
        using (var handler=new HttpClientHandler { CookieContainer=cookies,AllowAutoRedirect=false })
        using (var client=new HttpClient(handler)) {
            client.Timeout=TimeSpan.FromSeconds(20);
            return await RequestTicket(client,placeId,follow);
        }
    }
    public static async Task<string> RequestTicket(HttpClient client,ulong placeId,bool follow=false) {
        string csrf=null;
        for (int attempt=0; attempt<2; attempt++) {
            using (var request=new HttpRequestMessage(HttpMethod.Post,"https://auth.roblox.com/v1/authentication-ticket/")) {
                request.Headers.Referrer=new Uri(follow ? "https://www.roblox.com/users/"+placeId+"/profile" : "https://www.roblox.com/games/"+placeId);
                request.Headers.TryAddWithoutValidation("Origin","https://www.roblox.com");
                if (csrf!=null) request.Headers.TryAddWithoutValidation("X-CSRF-TOKEN",csrf);
                request.Content=new StringContent("");
                using (var response=await client.SendAsync(request)) {
                    System.Collections.Generic.IEnumerable<string> values;
                    if (response.IsSuccessStatusCode && response.Headers.TryGetValues("rbx-authentication-ticket",out values)) {
                        string ticket=values.FirstOrDefault();
                        if (!String.IsNullOrEmpty(ticket)) return ticket;
                    }
                    if (attempt==0 && response.StatusCode==HttpStatusCode.Forbidden && response.Headers.TryGetValues("x-csrf-token",out values)) { csrf=values.First(); continue; }
                    throw new LaunchException("Roblox ticket request returned HTTP "+(int)response.StatusCode+". Reopen the account and sign in again if its session expired.");
                }
            }
        }
        throw new LaunchException("Roblox did not issue a launch ticket.");
    }
    public static string BuildUri(string ticket,ulong placeId,bool follow=false) {
        string tracker=DateTime.UtcNow.Ticks.ToString();
        long timestamp=(long)(DateTime.UtcNow-new DateTime(1970,1,1)).TotalMilliseconds;
        string launcher=follow ? "https://assetgame.roblox.com/game/PlaceLauncher.ashx?request=RequestFollowUser&userId="+placeId : "https://assetgame.roblox.com/game/PlaceLauncher.ashx?request=RequestGame&browserTrackerId="+tracker+"&placeId="+placeId+"&isPlayTogetherGame=false";
        return "roblox-player:1+launchmode:play+gameinfo:"+Uri.EscapeDataString(ticket)+"+launchtime:"+timestamp+"+placelauncherurl:"+Uri.EscapeDataString(launcher)+"+browsertrackerid:"+tracker+"+robloxLocale:en_us+gameLocale:en_us+channel:+LaunchExp:InApp";
    }
    public static async Task<int> Check() {
        using (var handler=new FakeTicketHandler())
        using (var client=new HttpClient(handler)) {
            string ticket=await RequestTicket(client,12345);
            if (ticket!="synthetic-ticket" || handler.Calls!=2) return 6;
            string uri=BuildUri(ticket,12345);
            if (!uri.StartsWith("roblox-player:1+") || !Uri.UnescapeDataString(uri).Contains("placeId=12345")) return 7;
        }
        using (var client=new HttpClient(new FakeDeniedHandler())) {
            try { await RequestTicket(client,12345); return 8; } catch (LaunchException) { }
        }
        return 0;
    }
    class FakeTicketHandler : HttpMessageHandler {
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancel) {
            Calls++;
            var response=new HttpResponseMessage(Calls==1 ? HttpStatusCode.Forbidden : HttpStatusCode.OK);
            if (Calls==1) response.Headers.Add("x-csrf-token","test-csrf");
            else if (request.Headers.GetValues("X-CSRF-TOKEN").First()=="test-csrf") response.Headers.Add("rbx-authentication-ticket","synthetic-ticket");
            return Task.FromResult(response);
        }
    }
    class FakeDeniedHandler : HttpMessageHandler {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancel) { return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)); }
    }
}

