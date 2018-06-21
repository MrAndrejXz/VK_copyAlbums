using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.IO;

namespace VK
{
    class Program
    {
        static JavaScriptSerializer jSS = new JavaScriptSerializer();
        static string version = "5.80";
        static offset_struct offset;
        static token_struct token;
        static group_struct group;

        [Serializable]
        public struct group_struct
        {
            public string groupID_source;
            public string groupID_copy;
        }

        [Serializable]
        public struct token_struct
        {
            public string tokenApp;
            public string tokenGroup;
        }

        [Serializable]
        public struct offset_struct
        {
            public int offsetGroup;
            public int offsetPhoto;
            public string albumID;
        }

        static void Main(string[] args)
        {
            Func();
            GC.Collect();
            Console.WriteLine("Копирование завершено!");
            Console.ReadLine();
        }

        static string webRequestString(string req, string metod, string data)
        {
            string response;
            using (WebClient webClient = new WebClient() { Encoding = Encoding.UTF8 })
            {
                response=webClient.UploadString(req, metod, data);
            }
            return response;
        }

        static string webRequestFile(string req,string metod, string filepath)
        {
            string response;
            using (WebClient webClient = new WebClient() { Encoding = Encoding.UTF8 })
            {
                response = Encoding.UTF8.GetString(webClient.UploadFile(req, metod, filepath));
            }
            return response;
        }

        static void getAlbums()
        {
            int index = 0;
            var req = "https://api.vk.com/method/photos.getAlbums";
            var data = "&owner_id=" + group.groupID_source + "&v=" + version + "&access_token=" + token.tokenApp;
            var rez = webRequestString(req, "POST", data);
            dynamic obj = jSS.DeserializeObject(rez);
            Console.WriteLine("Найдено объектов: " + obj["response"]["count"]);
            var t = obj["response"]["items"][0];
            /*!!!!!!!!!!!!!!!!!!!!!С конца и удаление файла!!!!!!!!!!!!!!!!!!!!!!!!!!!!*/
            for (int i = obj["response"]["count"]-2; i >= 0;i--)
            {
                Console.WriteLine(++index + ".\t" + obj["response"]["items"][i]["size"] + "\t" + obj["response"]["items"][i]["title"]);
                if (offset.offsetGroup >= index) continue;
                saveAlbum(obj["response"]["items"][i]);
                offset.offsetGroup++;
                offset.offsetPhoto = 0;
                File.WriteAllText(@"Settings\offset.txt", jSS.Serialize(offset));
                
            }
        }

        static string createAlbum(string title)
        {
            var req = "https://api.vk.com/method/photos.createAlbum";
            var data = "&upload_by_admins_only=1"+"&title=" + title + "&group_id=" + group.groupID_copy+ "&v=" + version + "&access_token=" + token.tokenApp;
            dynamic rez = jSS.DeserializeObject(webRequestString(req, "POST", data));
            return rez["response"]["id"].ToString();
        }

        static void uploadToVk(string albumID,string path)
        {
            var req = "https://api.vk.com/method/photos.getUploadServer";
            var data = "&album_id=" + albumID + "&group_id=" + group.groupID_copy + "&v=" + version + "&access_token=" + token.tokenApp;
            //Получаем сервер
            dynamic serverResponse = jSS.DeserializeObject(webRequestString(req, "POST", data));
            //Загружаем на сервер
            serverResponse = jSS.DeserializeObject(webRequestFile(serverResponse["response"]["upload_url"],"POST", path));
            //Сохраняем
            req = "https://api.vk.com/method/photos.save";
            data = "&album_id=" + albumID + "&group_id=" + group.groupID_copy + "&photos_list=" + serverResponse["photos_list"] + "&server="+serverResponse["server"]+"&hash="+serverResponse["hash"]+"&access_token="+token.tokenApp+"&v="+version;
            var x=webRequestString(req, "POST", data);
        }

        static void saveAlbum(dynamic obj)
        {
            int count = offset.offsetPhoto;
            string path = obj["title"];
            var req = "https://api.vk.com/method/photos.get";
            var data = "&offset="+offset.offsetPhoto+"&owner_id=" + obj["owner_id"] + "&album_id=" + obj["id"] + "&v=" + version + "&access_token=" + token.tokenApp;
            var rez = webRequestString(req, "POST", data);
            string albumID;
            if (offset.offsetPhoto == 0)
            {
                albumID = createAlbum(path);
                offset.albumID = albumID;
            }
            else
                albumID = offset.albumID;
            obj = jSS.DeserializeObject(rez);

            string url="";
            int width=0;
            int height=0;
            // Скачиваем одну фотографию
            foreach (var item in obj["response"]["items"])
            {
                foreach (var itemSize in item["sizes"])
                {
                    width = 0;
                    height = 0;
                    if (itemSize["width"] > width && itemSize["height"] > height)
                    {
                        width = itemSize["width"];
                        height = itemSize["height"];
                        url = itemSize["url"];
                    }
                }
                using (WebClient client = new WebClient())
                {
                    try { 
                        client.DownloadFile(new Uri(url), @"Foto\"+item["id"]+".jpg");
                        uploadToVk(albumID, @"Foto\" + item["id"] + ".jpg");
                        File.Delete(@"Foto\" + item["id"] + ".jpg");
                    }
                    catch(Exception ex){
                        Console.WriteLine(ex.ToString());
                        getAlbums();
                    }
                }
                updateConsole("\t" + "Скачано: " + ++count + "/" + obj["response"]["count"]);
                offset.offsetPhoto++;
                File.WriteAllText(@"Settings\offset.txt", jSS.Serialize(offset));
            }
        }

        static void updateConsole(string txt)
        {
            var y = Console.CursorTop;
            Console.WriteLine(txt);
            Console.SetCursorPosition(0, y);
        }
        static void Func()
        {
            group=jSS.Deserialize<group_struct>(File.ReadAllText(@"Settings\group"));
            token = jSS.Deserialize<token_struct>(File.ReadAllText(@"Settings\token"));
            offset = jSS.Deserialize<offset_struct>(File.ReadAllText(@"Settings\offset"));
            getAlbums();
        }

    }
}