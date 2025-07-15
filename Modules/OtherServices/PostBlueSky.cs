using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Epic.OnlineServices.Auth;

namespace TownOfHostForE.Modules.OtherServices
{
    public class PostBlueSky : IDisposable
    {

        #region 定数

        private static readonly string POST_DATA_PATH = @"./TOH_DATA/BlueSkyPost.txt";
        private static readonly string USER_SETTING_PATH = @"./TOH_DATA/BlueSkyUserInfo.csv";

        #endregion

        #region メンバ変数

        string _handle = "";
        string _pass = "";
        string _postMessage = "";

        #endregion

        #region プロパティ

        /// <summary>
        /// BlueSkyボタン表示プロパティ
        /// </summary>
        public bool ViewBlueSkyButton
        {
            get
            {
                return !string.IsNullOrEmpty(this._handle) &&
                       !string.IsNullOrEmpty(this._pass) &&
                       !string.IsNullOrEmpty(this._postMessage);
            }
        }

        #endregion

        #region 列挙型
        private enum RoomValues
        {
            RoomCode,
            Map,
            Teams,
            Players
        }

        #endregion

        #region コンストラクタ

        public PostBlueSky()
        {
            this.SetUserInfo();
            this._postMessage = this.ReadPostData();
        }

        #endregion

        #region デストラクタ
        public void Dispose() { }
        #endregion

        #region 公的メソッド

        /// <summary>
        /// 募集文言を青空へポスト
        /// </summary>
        public void PostRecruit()
        {
            if (Main.EnableBlueSkyPost.Value == false)
            {
                return;
            }

            if (!string.IsNullOrEmpty(this._postMessage))
            {
                var message = this.ReplaceKeywords(this._postMessage);

                //ラストにハッシュタグ付け
                message += "\n#TOH4E";

                this.BSPost(message);
            }
        }
        #endregion

        #region 私的メソッド
        /// <summary>
        /// ユーザー上方読み取りメソッド
        /// </summary>
        private void SetUserInfo()
        {
            try
            {
                string[] lines = System.IO.File.ReadAllLines(USER_SETTING_PATH);

                if (lines.Length >= 2)
                {
                    this._handle = lines[0];
                    this._pass = lines[1];
                }
                else
                {
                    Logger.Info("アカウントの設定ファイルに2行未満しか存在しません。","BlueSky");
                }
            }
            catch (Exception ex)
            {
                Logger.Info("アカウントの設定ファイル読み込みにてエラーが発生しました: " + ex.Message,"BlueSky");
            }
        }

        /// <summary>
        /// 変数展開メソッド
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        private string ReplaceKeywords(string message)
        {
            //ルームコード置換
            if (message.Contains(CreateEnumWord(RoomValues.RoomCode)))
            {
                string roomCode = InnerNet.GameCode.IntToGameName(AmongUsClient.Instance.GameId);
                message = message.Replace(CreateEnumWord(RoomValues.RoomCode),roomCode);
            }

            //マップ置換
            if (message.Contains(CreateEnumWord(RoomValues.Map)))
            {
                string mapName = Constants.MapNames[Main.NormalOptions.MapId];
                message = message.Replace(CreateEnumWord(RoomValues.Map), mapName);
            }

            //陣営置換
            if (message.Contains(CreateEnumWord(RoomValues.Teams)))
            {
                string teams = CreateTeamsString();
                message = message.Replace(CreateEnumWord(RoomValues.Teams), teams);
            }

            //人数置換
            if (message.Contains(CreateEnumWord(RoomValues.Players)))
            {
                string players = $"{GameData.Instance.PlayerCount}/{GameManager.Instance.LogicOptions.currentGameOptions.MaxPlayers}";
                message = message.Replace(CreateEnumWord(RoomValues.Players), players);
            }


            return message;
        }

        /// <summary>
        /// 変数置換メソッド
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        private string CreateEnumWord(RoomValues value)
        {
            return "{{" + value.ToString() + "}}";
        }

        /// <summary>
        /// 現在設定されているチーム情報取得メソッド
        /// </summary>
        /// <returns></returns>
        private string CreateTeamsString()
        {

            bool assignCrew = false;
            bool assignMad = false;
            bool assignImp = false;
            bool assignThird = false;
            bool assignAnimals = false;
            foreach (var role in CustomRolesHelper.AllStandardRoles)
            {
                if (role.IsEnable())
                {
                    if (role.IsCrewmate())
                    {
                        assignCrew = true;
                    }
                    if (role.IsImpostor())
                    {
                        assignImp = true;
                    }
                    if (role.IsMadmate())
                    {
                        assignMad = true;
                    }
                    if (role.IsNeutral())
                    {
                        assignThird = true;
                    }
                    if (role.IsAnimals())
                    {
                        assignAnimals = true;
                    }
                }
            }

            string returns = "";

            if (assignCrew)
            {
                returns += "Crew";
            }
            if (assignImp)
            {
                returns = SetTeamWord(returns,"Imp");
            }
            if (assignMad)
            {
                returns = SetTeamWord(returns, "Mad");
            }
            if (assignThird)
            {
                returns = SetTeamWord(returns, "Neu");
            }
            if (assignAnimals)
            {
                returns = SetTeamWord(returns, "Anim");
            }

            return returns;
        }

        /// <summary>
        /// 陣営文字列結合メソッド
        /// </summary>
        /// <param name="message"></param>
        /// <param name="teamName"></param>
        /// <returns></returns>
        private string SetTeamWord(string message,string teamName)
        {
            if (!string.IsNullOrEmpty(message))
            {
                message += "/";
            }
            message += teamName;

            return message;
        }

        /// <summary>
        /// 青空ポスト内容取得
        /// </summary>
        /// <exception cref="Exception"></exception>
        private string ReadPostData()
        {
            try
            {
                string fileContent = System.IO.File.ReadAllText(POST_DATA_PATH);
                Logger.Msg("ポスト内容読み取り終了", "BlueSky");
                return fileContent;

            }
            catch (Exception e)
            {
                //throw new Exception("ポスト内容読み取り例外：" + e.Message + "/" + e.StackTrace);
                return null;
            }
        }
        /// <summary>
        /// 青空へポスト
        /// </summary>
        /// <param name="message"></param>
        private async void BSPost(string message)
        {
            //ハンドルもしくはパスが空
            if (string.IsNullOrEmpty(this._handle) ||
                string.IsNullOrEmpty(this._pass))
            {
                Logger.Info($"アカウント設定が未入力です", "BlueSky");
                return;
            }

            //メッセージが300文字を越える場合
            if (message.Length > 300)
            {
                Logger.Info($"post内容が長すぎるため投稿出来ませんでした。", "BlueSky");
                return;
            }

            bool success = await BlueskyPoster.PostToBlueskyAsync(this._handle, this._pass, message);
            if (success)
            {
                Logger.Info($"ポストしたよ「{message}」", "BlueSky");
            }
            else
            {
                Logger.Info($"ポストに失敗したよ「{message}」", "BlueSky");
            }

        }
        #endregion
    }

    public class BlueskyPoster
    {
        class AuthResponse
        {
            public string accessJwt { get; set; }
            public string did { get; set; }
        }

        /// <summary>
        /// 青空へポストする本チャン
        /// </summary>
        /// <param name="handle"></param>
        /// <param name="password"></param>
        /// <param name="postText"></param>
        /// <returns></returns>
        public static async Task<bool> PostToBlueskyAsync(string handle, string password, string postText)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    // 認証（セッション作成）
                    var loginPayload = new Dictionary<string, object>()
                    {
                        { "identifier", handle },
                        { "password", password }
                    };

                    // JSONシリアライズ
                    var loginJson = JsonHelper.SerializeDictionary(loginPayload);

                    var loginContent = new StringContent(loginJson, Encoding.UTF8, "application/json");

                    var loginResponse = await client.PostAsync("https://bsky.social/xrpc/com.atproto.server.createSession", loginContent);
                    var loginBody = await loginResponse.Content.ReadAsStringAsync();

                    if (!loginResponse.IsSuccessStatusCode)
                    {
                        Logger.Info("ログインに失敗したためポスト出来ませんでした。", "BlueSky");
                        return false;
                    }

                    var auth = JsonHelper.DeserializeObject<AuthResponse>(loginBody);

                    // 投稿データ作成
                    //var postPayload = new Dictionary<string, object>()
                    //{
                    //    { "repo", auth.did },
                    //    { "collection", "app.bsky.feed.post" },
                    //    { "record", new Dictionary<string,object>()
                    //        {
                    //            {"@type" , "app.bsky.feed.post"},
                    //            {"text" , postText},
                    //            {"createdAt" , DateTime.UtcNow.ToString("o")}
                    //        }
                    //    }
                    //};

                    var postPayload = BuildPostPayload(postText,auth.did);

                    var postJson = JsonHelper.SerializeDictionary(postPayload);
                    var postContent = new StringContent(postJson, Encoding.UTF8, "application/json");

                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.accessJwt);

                    var postResponse = await client.PostAsync("https://bsky.social/xrpc/com.atproto.repo.createRecord", postContent);
                    var postBody = await postResponse.Content.ReadAsStringAsync();

                    if (!postResponse.IsSuccessStatusCode)
                    {
                        Logger.Info("ポストに失敗しました。", "BlueSky");
                        return false;
                    }

                    Logger.Info("✅ 投稿成功！", "BlueSky");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.Info($"\"例外発生: \" + {ex.Message}", "BlueSky");
                return false;
            }
        }

        private static Dictionary<string, object> BuildPostPayload(string postText, string did)
        {
            var record = new Dictionary<string, object>
        {
            { "@type", "app.bsky.feed.post" },
            { "text", postText },
            { "createdAt", DateTime.UtcNow.ToString("o") }
        };

            // ハッシュタグから facets を生成
            var facets = GenerateFacets(postText);
            if (facets.Count > 0)
            {
                record["facets"] = facets;
            }

            // 投稿全体を構築
            var postPayload = new Dictionary<string, object>
        {
            { "repo", did },
            { "collection", "app.bsky.feed.post" },
            { "record", record }
        };

            return postPayload;
        }

        private static List<Dictionary<string, object>> GenerateFacets(string text)
        {
            var facets = new List<Dictionary<string, object>>();
            var regex = new Regex(@"#([^\s　\n\r\t]+)", RegexOptions.Compiled);

            foreach (Match match in regex.Matches(text))
            {
                string tagWithHash = match.Value;
                string tag = tagWithHash.Substring(1);
                int charStart = match.Index;

                int byteStart = Encoding.UTF8.GetByteCount(text.Substring(0, charStart));
                int byteEnd = byteStart + Encoding.UTF8.GetByteCount(tagWithHash);

                var facet = new Dictionary<string, object>
            {
                {
                    "index", new Dictionary<string, int>
                    {
                        { "byteStart", byteStart },
                        { "byteEnd", byteEnd }
                    }
                },
                {
                    "features", new List<Dictionary<string, object>>
                    {
                        new Dictionary<string, object>
                        {
                            { "$type", "app.bsky.richtext.facet#tag" },
                            { "tag", tag }
                        }
                    }
                }
            };

                facets.Add(facet);
            }

            return facets;
        }

        public static class JsonHelper
        {
            public static string SerializeDictionary(Dictionary<string, object> data)
            {
                return SerializeValue(data);
            }
            private static string SerializeValue(object value)
            {
                if (value == null)
                    return "null";

                if (value is string s)
                    return $"\"{EscapeJson(s)}\"";

                if (value is bool b)
                    return b.ToString().ToLower();

                if (value is int i)
                    return i.ToString(System.Globalization.CultureInfo.InvariantCulture);

                if (value is long l)
                    return l.ToString(System.Globalization.CultureInfo.InvariantCulture);

                if (value is float f)
                    return f.ToString(System.Globalization.CultureInfo.InvariantCulture);

                if (value is double d)
                    return d.ToString(System.Globalization.CultureInfo.InvariantCulture);

                if (value is decimal dec)
                    return dec.ToString(System.Globalization.CultureInfo.InvariantCulture);

                // 汎用 Dictionary（string以外の型含む）
                if (value is IDictionary dictObj)
                {
                    var kvParts = new List<string>();
                    foreach (DictionaryEntry entry in dictObj)
                    {
                        string key = EscapeJson(entry.Key.ToString());
                        string val = SerializeValue(entry.Value);
                        kvParts.Add($"\"{key}\":{val}");
                    }
                    return "{" + string.Join(",", kvParts) + "}";
                }

                // 汎用 List<object> または配列
                if (value is IEnumerable enumerable && !(value is string))
                {
                    var items = new List<string>();
                    foreach (var item in enumerable)
                    {
                        items.Add(SerializeValue(item));
                    }
                    return "[" + string.Join(",", items) + "]";
                }

                // fallback: ToStringで文字列化
                return $"\"{EscapeJson(value.ToString())}\"";
            }

            private static string EscapeJson(string input)
            {
                if (string.IsNullOrEmpty(input)) return "";
                return input.Replace("\\", "\\\\")
                            .Replace("\"", "\\\"")
                            .Replace("\n", "\\n")
                            .Replace("\r", "\\r")
                            .Replace("\t", "\\t");
            }


            private static string UnescapeJson(string s)
            {
                return s.Replace("\\\"", "\"")
                        .Replace("\\\\", "\\")
                        .Replace("\\n", "\n")
                        .Replace("\\r", "\r")
                        .Replace("\\t", "\t");
            }



            public static T DeserializeObject<T>(string json) where T : new()
            {
                var dict = JsonHelper.DeserializeToDictionary(json); // 前回のメソッドを使う
                return ConvertDictionaryToObject<T>(dict);
            }
            private static Dictionary<string, object> DeserializeToDictionary(string json)
            {
                var result = new Dictionary<string, object>();

                if (string.IsNullOrWhiteSpace(json)) return result;

                // 前後の中かっこを除去
                json = json.Trim().TrimStart('{').TrimEnd('}');

                // カンマで区切って key-value を処理
                var pairs = json.Split(',');

                foreach (var pair in pairs)
                {
                    var kv = pair.Split(new[] { ':' }, 2);
                    if (kv.Length != 2) continue;

                    var key = UnescapeJson(kv[0].Trim().Trim('"'));
                    var rawValue = kv[1].Trim();

                    object value;
                    if (rawValue.StartsWith("\"") && rawValue.EndsWith("\""))
                    {
                        value = UnescapeJson(rawValue.Trim('"'));
                    }
                    else if (rawValue == "true" || rawValue == "false")
                    {
                        value = rawValue == "true";
                    }
                    else if (double.TryParse(rawValue, out var number))
                    {
                        value = number;
                    }
                    else
                    {
                        value = rawValue;
                    }

                    result[key] = value;
                }

                return result;
            }
            private static T ConvertDictionaryToObject<T>(Dictionary<string, object> dict) where T : new()
            {
                T obj = new T();
                var type = typeof(T);

                foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (dict.TryGetValue(prop.Name, out var value))
                    {
                        try
                        {
                            if (value != null)
                            {
                                var targetType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                                object safeValue = Convert.ChangeType(value, targetType);
                                prop.SetValue(obj, safeValue);
                            }
                        }
                        catch
                        {
                            // 値の変換に失敗してもスキップ
                        }
                    }
                }

                return obj;
            }
        }
    }
}
