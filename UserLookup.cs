using System;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
public static class UserLookup {
    public static string Normalize(string username) {
        username=(username??"").Trim().TrimStart('@');
        if(!Regex.IsMatch(username,@"\A[A-Za-z0-9_]{3,20}\z")) throw new LaunchException("Enter the Roblox username, not the display name.");
        return username;
    }
    public static ulong Parse(string json,string username) {
        var result=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(json);
        if(!result.ContainsKey("data")) throw new LaunchException("Unexpected username response.");
        var sequence=result["data"] as IEnumerable;
        var records=sequence==null?null:sequence.Cast<object>().ToArray();
        if(records==null || records.Length!=1) throw new LaunchException("That username was not found or is unavailable.");
        var user=records[0] as Dictionary<string,object>; ulong id;
        if(user==null || !user.ContainsKey("id") || !user.ContainsKey("name") || !String.Equals(Convert.ToString(user["name"]),username,StringComparison.OrdinalIgnoreCase) || !UInt64.TryParse(Convert.ToString(user["id"]),out id) || id==0) throw new LaunchException("Roblox could not confirm the username.");
        return id;
    }
    public static async Task<ulong> Resolve(string username) {
        username=Normalize(username);
        using(var client=new HttpClient()) {
            client.Timeout=TimeSpan.FromSeconds(15);
            string body=new JavaScriptSerializer().Serialize(new { usernames=new[] {username},excludeBannedUsers=true });
            using(var content=new StringContent(body,Encoding.UTF8,"application/json"))
            using(var response=await client.PostAsync("https://users.roblox.com/v1/usernames/users",content)) {
                if(!response.IsSuccessStatusCode) throw new LaunchException("Username lookup returned HTTP "+(int)response.StatusCode+". Try again later.");
                return Parse(await response.Content.ReadAsStringAsync(),username);
            }
        }
    }
}
