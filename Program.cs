using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml;
using HtmlAgilityPack;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InlineQueryResults;
using Telegram.Bot.Types.ReplyMarkups;

namespace rate_news
{
    class Program
    {
        private static TelegramBotClient botClient;
        private static int newsCounter = 0;
        private static List<string> allNewsLinks = new List<string>();

        static void Main()
        {
            botClient = new TelegramBotClient(Config.TelegramApiToken);

            botClient.OnMessage += Bot_OnMessage;
            botClient.OnCallbackQuery += Bot_OnCallbackQuery;
            botClient.StartReceiving();

            Console.WriteLine("Bot started. Press any key to exit.");
            Console.ReadKey();

            botClient.StopReceiving();
        }

        private static async void Bot_OnMessage(object sender, MessageEventArgs e)
        {
            if (e.Message.Type == MessageType.Text)
            {
                if (e.Message.Text == "/start")
                {
                    await botClient.SendTextMessageAsync(e.Message.Chat.Id,
                        "Привет! Нажми кнопку, чтобы получить курс валют или новости.",
                        replyMarkup: GetInlineKeyboard());
                }
                else if (e.Message.Text == "/news")
                {
                    await SendNewsLinks(e.Message.Chat.Id);
                }
            }
        }

        private static async void Bot_OnCallbackQuery(object sender, CallbackQueryEventArgs e)
        {
            if (e.CallbackQuery.Data == "getRate")
            {
                string rate = await GetDollarToRubleRate();
                await botClient.SendTextMessageAsync(e.CallbackQuery.Message.Chat.Id, rate);
            }
            else if (e.CallbackQuery.Data == "getNews")
            {
                await SendNewsLinks(e.CallbackQuery.Message.Chat.Id);
            }
            else if (e.CallbackQuery.Data == "nextNews")
            {
                newsCounter += 3;
                await SendNewsLinks(e.CallbackQuery.Message.Chat.Id);
            }
            else if (e.CallbackQuery.Data == "previousNews")
            {
                newsCounter -= 3;
                await SendNewsLinks(e.CallbackQuery.Message.Chat.Id);
            }
            else if (e.CallbackQuery.Data == "goHome")
            {
                await botClient.SendTextMessageAsync(e.CallbackQuery.Message.Chat.Id,
                    "Вы вернулись на главную страницу.",
                    replyMarkup: GetInlineKeyboard());
            }
        }

        private static async Task<List<string>> GetNewsLinks()
        {
            List<string> newsLinks = new List<string>();
            string newsUrl = "https://wylsa.com/category/news/";

            using (HttpClient client = new HttpClient())
            {
                try
                {
                    HttpResponseMessage response = await client.GetAsync(newsUrl);

                    if (response.IsSuccessStatusCode)
                    {
                        string html = await response.Content.ReadAsStringAsync();

                        HtmlDocument doc = new HtmlDocument();
                        doc.LoadHtml(html);

                        var newsNodes = doc.DocumentNode.SelectNodes("//a[contains(@class, 'postCard-wrapper')]");

                        if (newsNodes != null && newsNodes.Count > 0)
                        {
                            for (int i = 0; i < newsNodes.Count; i++)
                            {
                                string newsLink = newsNodes[i].GetAttributeValue("href", "");
                                newsLinks.Add(newsLink);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Обработка ошибок при запросе к API
                }
            }

            return newsLinks;
        }

        private static async Task SendNewsLinks(long chatId)
        {
            if (allNewsLinks.Count == 0)
            {
                allNewsLinks = await GetNewsLinks();
            }

            if (allNewsLinks.Count > 0)
            {
                int startIndex = newsCounter;
                int endIndex = startIndex + 3;

                for (int i = startIndex; i < Math.Min(endIndex, allNewsLinks.Count); i++)
                {
                    await botClient.SendTextMessageAsync(chatId, allNewsLinks[i]);
                }

                List<InlineKeyboardButton> inlineButtons = new List<InlineKeyboardButton>();

                if (endIndex < allNewsLinks.Count)
                {
                    inlineButtons.Add(InlineKeyboardButton.WithCallbackData("Далее", "nextNews"));
                }

                if (startIndex > 0)
                {
                    inlineButtons.Add(InlineKeyboardButton.WithCallbackData("Назад", "previousNews"));
                }

                inlineButtons.Add(InlineKeyboardButton.WithCallbackData("Домой", "goHome"));

                InlineKeyboardMarkup inlineKeyboard = new InlineKeyboardMarkup(inlineButtons);
                await botClient.SendTextMessageAsync(chatId, "Выберите действие:", replyMarkup: inlineKeyboard);
            }
            else
            {
                await botClient.SendTextMessageAsync(chatId, "Не удалось получить ссылки на новости.");
            }
        }


        private static async Task<string> GetDollarToRubleRate()
        {
            string apiUrl = "https://www.cbr-xml-daily.ru/daily_utf8.xml";

            using (HttpClient client = new HttpClient())
            {
                try
                {
                    HttpResponseMessage response = await client.GetAsync(apiUrl);

                    if (response.IsSuccessStatusCode)
                    {
                        string xml = await response.Content.ReadAsStringAsync();
                        XmlDocument xmlDoc = new XmlDocument();
                        xmlDoc.LoadXml(xml);
                        XmlElement root = xmlDoc.DocumentElement;
                        XmlNode node = root.SelectSingleNode("//Valute[CharCode='USD']/Value");
                        XmlNode eur = root.SelectSingleNode("//Valute[CharCode='EUR']/Value");
                        XmlNode cny = root.SelectSingleNode("//Valute[CharCode='CNY']/Value");
                        double rate = Convert.ToDouble(node.InnerText.Replace(",", "."));
                        double rid = Convert.ToDouble(eur.InnerText.Replace(",", "."));
                        double CNY = Convert.ToDouble(cny.InnerText.Replace(",", "."));

                        return $"1 Доллар США = {rate} рублей\n" +
                            $"1 Евро = {rid} рублей\n" +
                            $"1 Китайский юань = {CNY} рублей";
                    }
                    else
                    {
                        return "Не удалось получить данные о курсе доллара к рублю";
                    }
                }
                catch (Exception ex)
                {
                    // Обработка ошибок при запросе к API
                    return "Произошла ошибка при получении данных о курсе доллара к рублю";
                }
            }
        }

        private static InlineKeyboardMarkup GetInlineKeyboard()
        {
            var inlineKeyboard = new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("Получить курс", "getRate"),
                    InlineKeyboardButton.WithCallbackData("Получить новости", "getNews")
                },

            });

            return inlineKeyboard;
        }

        private static InlineKeyboardMarkup GetNextInlineKeyboard()
        {
            var inlineKeyboard = new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("Далее", "nextNews")
                }
            });

            return inlineKeyboard;
        }

        private static InlineKeyboardMarkup GetPreviousInlineKeyboard()
        {
            var inlineKeyboard = new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("Назад", "previousNews")
                }
            });

            return inlineKeyboard;
        }

        private static InlineKeyboardMarkup GetHomeInlineKeyboard()
        {
            var inlineKeyboard = new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("Домой", "goHome")
                }
            });

            return inlineKeyboard;
        }
    }
}
