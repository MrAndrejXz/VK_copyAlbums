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

        /// <summary>
        /// Структура, хранящая ID групп
        /// </summary>
        [Serializable]
        public struct group_struct
        {
            public string groupID_source;
            public string groupID_copy;
        }

        /// <summary>
        /// Структура, хранящая токены
        /// </summary>
        [Serializable]
        public struct token_struct
        {
            public string tokenApp;
            public string tokenGroup;
        }

        /// <summary>
        /// Структура, хранящая смещения
        /// </summary>
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

        /// <summary>
        /// Выгружает строку на указаный ресурс
        /// </summary>
        /// <param name="req">URL ресурса</param>
        /// <param name="metod">Метод передачи</param>
        /// <param name="data">Выгружаемая строка</param>
        /// <returns></returns>
        static string webRequestString(string req, string metod, string data)
        {
            string response;
            using (WebClient webClient = new WebClient() { Encoding = Encoding.UTF8 })
            {
                response=webClient.UploadString(req, metod, data);
            }
            return response;
        }

        /// Выгружает файл на указаный ресурс
        /// </summary>
        /// <param name="req">URL ресурса</param>
        /// <param name="metod">Метод передачи</param>
        /// <param name="data">Выгружаемая строка</param>
        /// <returns></returns>
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
            //Получаем список альбомов
            dynamic obj = jSS.DeserializeObject(webRequestString("https://api.vk.com/method/photos.getAlbums", "POST", "&owner_id=" + group.groupID_source + "&v=" + version + "&access_token=" + token.tokenApp));
            Console.WriteLine("Найдено объектов: " + obj["response"]["count"]);
            //Идём по альбомам с конца
            for (int i = obj["response"]["count"]-2; i >= 0;i--)
            {
                //Выводим текущий
                Console.WriteLine(++index + ".\t" + obj["response"]["items"][i]["size"] + "\t" + obj["response"]["items"][i]["title"]);
                //Проверяем, если его скачивали, то переходим к следующему
                if (offset.offsetGroup >= index) continue;
                //Сохраняем альбом
                saveAlbum(obj["response"]["items"][i]);
                offset.offsetGroup++;
                offset.offsetPhoto = 0;
                File.WriteAllText(@"Settings\offset.txt", jSS.Serialize(offset));
            }
        }

        /// <summary>
        /// Создание альбома
        /// </summary>
        /// <param name="title">Название альбома</param>
        static string createAlbum(string title)
        {
            dynamic rez = jSS.DeserializeObject(webRequestString("https://api.vk.com/method/photos.createAlbum", "POST", "&upload_by_admins_only=1" + "&title=" + title + "&group_id=" + group.groupID_copy + "&v=" + version + "&access_token=" + token.tokenApp));
            return rez["response"]["id"].ToString();
        }

        /// <summary>
        /// Загрузка фото в альбом
        /// </summary>
        /// <param name="albumID">id альбома</param>
        /// <param name="path">Путь к файлу</param>
        static void uploadToVk(string albumID,string path)
        {
            //Получаем сервер
            dynamic serverResponse = jSS.DeserializeObject(webRequestString("https://api.vk.com/method/photos.getUploadServer", "POST", "&album_id=" + albumID + "&group_id=" + group.groupID_copy + "&v=" + version + "&access_token=" + token.tokenApp));
            //Загружаем на сервер
            serverResponse = jSS.DeserializeObject(webRequestFile(serverResponse["response"]["upload_url"],"POST", path));
            //Сохраняем
            var x=webRequestString("https://api.vk.com/method/photos.save", "POST", "&album_id=" + albumID + "&group_id=" + group.groupID_copy + "&photos_list=" + serverResponse["photos_list"] + "&server="+serverResponse["server"]+"&hash="+serverResponse["hash"]+"&access_token="+token.tokenApp+"&v="+version);
        }

        /// <summary>
        /// Сохранение альбома
        /// </summary>
        /// <param name="obj"></param>
        static void saveAlbum(dynamic obj)
        {
            int count = offset.offsetPhoto;
            string path = obj["title"];
            string albumID;
            //Создавали альбом
            if (offset.offsetPhoto == 0)
            {
                //Создаем альбом
                albumID = createAlbum(path);
                offset.albumID = albumID;
            }
            else
                albumID = offset.albumID;
            obj = jSS.DeserializeObject(webRequestString("https://api.vk.com/method/photos.get", "POST", "&offset=" + offset.offsetPhoto + "&owner_id=" + obj["owner_id"] + "&album_id=" + obj["id"] + "&v=" + version + "&access_token=" + token.tokenApp));
            
            //Поиск картинки наибольшего размера
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
                //Скачиваем фотографию
                using (WebClient client = new WebClient())
                {
                    try { 
                        //Скачиваем
                        client.DownloadFile(new Uri(url), @"Foto\"+item["id"]+".jpg");
                        //Загружаем в новый альбом
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

        /// <summary>
        /// Печать на предыдущей строке
        /// </summary>
        /// <param name="txt">Текст печати</param>
        static void updateConsole(string txt)
        {
            var y = Console.CursorTop;
            Console.WriteLine(txt);
            Console.SetCursorPosition(0, y);
        }

        static void Func()
        {
            //Инициализация
            group=jSS.Deserialize<group_struct>(File.ReadAllText(@"Settings\group"));
            token = jSS.Deserialize<token_struct>(File.ReadAllText(@"Settings\token"));
            offset = jSS.Deserialize<offset_struct>(File.ReadAllText(@"Settings\offset"));
            getAlbums();
        }

    }
}