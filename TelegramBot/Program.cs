using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Threading;
using static System.TimeZoneInfo;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using System.IO;
using System.Data.Common;
using System.Text;
using Newtonsoft.Json.Linq;

namespace GoogleCalendarApi
{
    class Program
    {
        public static class ScheduleParser
        {
            private static string[] Scopes = { CalendarService.Scope.CalendarReadonly };
            private static CalendarService? Service = null;

            static ScheduleParser()
            {
                try
                {
                    var credential = GoogleCredential.FromFile("C:\\Users\\Sasha\\Desktop\\TelegramBot/telegrambot-363818-c5afa8c735d4.json")
                                    .CreateScoped(Scopes);
                    Service = new CalendarService(new BaseClientService.Initializer()
                    {
                        HttpClientInitializer = credential,
                        ApplicationName = "Your Application Name"
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                    throw;
                }
            }

            public static async Task<List<Event>> GetScheduleForGroup(string group, DateTime date)
            {
                var calendarId = await GetCalendarIdForGroup(group);

                if (calendarId == null)
                {
                    return null;
                }

                var events = await GetEventsForDate(calendarId, date);

                if (events == null || events.Count == 0)
                {
                    return null;
                }

                return events;
            }

            private static async Task<List<Event>> GetEventsForDate(string calendarId, DateTime date)
            {
                EventsResource.ListRequest request = Service.Events.List(calendarId);
                request.TimeMin = new DateTime(date.Year, date.Month, date.Day, 0, 0, 0);
                request.TimeMax = new DateTime(date.Year, date.Month, date.Day, 23, 59, 59);
                request.ShowDeleted = false;
                request.SingleEvents = true;
                request.MaxResults = 100;
                request.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;

                var response = await request.ExecuteAsync();

                return response.Items.Cast<Event>().ToList();
            }

            private static async Task<string> GetCalendarIdForGroup(string group)
            {
                var calendarList = await Service.CalendarList.List().ExecuteAsync();
                var calendar = calendarList.Items.FirstOrDefault(c => c.Id == group);

                if (calendar == null)
                {
                    Console.WriteLine($"Календар з ідентифікатором {group} не знайдено.");
                    return null;
                }

                return calendar.Id;
            }
        }
    
    

    private static async Task<string> GetSchedule(string groupName, DateTime date)
        {
            // Створення об'єкту клієнта для роботи з Google Calendar API
            var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                new ClientSecrets
                {
                    ClientId = "403794239920-8bte1nq4kusub13f0odighk29bpqfn97.apps.googleusercontent.com",
                    ClientSecret = "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"
                },
                new[] { CalendarService.Scope.CalendarReadonly },
                "user",
                CancellationToken.None);

            var service = new CalendarService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "TelegramBot"
            });

            // Формування запиту до Google Calendar API для отримання подій за датою та групою
            var calendarId = "c_hlnnlo9824qj9tc1ita1ofhgu8@group.calendar.google.com";
            var request = service.Events.List(calendarId);
            request.TimeMin = date.Date;
            request.TimeMax = date.AddDays(1).Date;
            request.SingleEvents = true;
            request.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;

            // Виконання запиту та обробка результату
            var events = await request.ExecuteAsync();
            var sb = new StringBuilder();

            if (events.Items != null && events.Items.Any())
            {
                sb.AppendLine($"Розклад для {groupName} на {date:dd.MM.yyyy}:");
                sb.AppendLine();

                foreach (var item in events.Items)
                {
                    var start = item.Start.DateTime.HasValue ? item.Start.DateTime.Value : DateTime.Parse(item.Start.Date);
                    sb.AppendLine($"Предмет: {item.Summary}");
                    sb.AppendLine($"Час: {start:HH:mm} - {start.AddMinutes(item.End.DateTime.Value.Minute - start.Minute):HH:mm}");
                    sb.AppendLine($"Аудиторія: {item.Location}");
                    sb.AppendLine();
                }
            }
            else
            {
                sb.AppendLine($"На {date:dd.MM.yyyy} для {groupName} немає розкладу.");
            }

            return sb.ToString();
        }





        



        public static class ScheduleFormatter
        {
            public static string FormatSchedule(List<Event> schedule, DateTime date)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"Розклад на {date.ToShortDateString()}:");
                foreach (var lesson in schedule)
                {
                    sb.AppendLine($"{lesson.StartTime.ToString("HH:mm")} - {lesson.EndTime.ToString("HH:mm")}: {lesson.Subject} ({lesson.Teacher}) - {lesson.Room}");
                }
                return sb.ToString();
            }
        }

        public class Event
        {
            public DateTime StartTime { get; set; }
            public DateTime EndTime { get; set; }
            public string? Subject { get; set; }
            public string? Teacher { get; set; }
            public string? Room { get; set; }
        }






        private static async Task<string> GetSchedule(long chatId, DateTime date)
        {
            var group = userGroups[chatId];
            var schedule = await ScheduleParser.GetScheduleForGroup(group, date);
            if (schedule == null)
            {
                return "На цю дату розклад не знайдено для групи " + group;
            }
            return ScheduleFormatter.FormatSchedule(schedule, date);
        }


        private static async Task<string> GetScheduleForNextFiveDays(long chatId)
        {
            var group = userGroups[chatId];
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < 5; i++)
            {
                DateTime date = DateTime.Today.AddDays(i);
                var schedule = await ScheduleParser.GetScheduleForGroup(group, date);
                if (schedule == null)
                {
                    sb.AppendLine($"На {date.ToShortDateString()} розклад не знайдено для групи {group}");
                }
                else
                {
                    sb.AppendLine(ScheduleFormatter.FormatSchedule(schedule, date));
                }
            }
            return sb.ToString();
        }


        




        //метод для створення клавіатур для  головного меню
        private static ReplyKeyboardMarkup GetMainKeyboard()
        {
            var keyboard = new ReplyKeyboardMarkup(new[]
            {
        new[]
        {
            new KeyboardButton("Вивести розклад на сьогодні"),
            new KeyboardButton("Вивести розклад на завтра"),
        },
        new[]
        {
            new KeyboardButton("Вивести розклад на наступні 5 днів"),
            new KeyboardButton("Змінити групу"),
        },
    });
            return keyboard;
        }


        //метод для створення клавіатур для вибору групи
        private static ReplyKeyboardMarkup GetGroupKeyboard()
        {
            var keyboard = new ReplyKeyboardMarkup(new[]
            {
        new[]
        {
            new KeyboardButton("ПІ-91"),
            new KeyboardButton("КН-91"),
            new KeyboardButton("ІН-91")
        },
        new[]
        {
            new KeyboardButton("ПІ-20"),
            new KeyboardButton("КН-20"),
            new KeyboardButton("ІН-20")
        },
        new[]
        {
            new KeyboardButton("ПІ-21"),
            new KeyboardButton("КН-21"),
            new KeyboardButton("ІН-21")
        },
        new[]
        {
            new KeyboardButton("ПІ-22"),
            new KeyboardButton("КН-22"),
            new KeyboardButton("ІН-22")
        }
    });

            return keyboard;
        }

        // Словарь для хранения выбранной пользователем группы и его chat id
        static Dictionary<long, string> userGroups = new Dictionary<long, string>();
        // Путь к файлу для сохранения и чтения словаря
        static string filePath = "userGroup.txt";


        static void SaveDictionaryToFile(Dictionary<long, string> dictionary, string filePath)
        {
            using (StreamWriter writer = new StreamWriter(filePath))
            {
                foreach (var kvp in dictionary)
                {
                    writer.WriteLine($"{kvp.Key}:{kvp.Value}");
                }
            }
        }

        //токен для телеграм бота
        private static string token { get; set; } = "5522982120:AAFwClh10_pQJyvAvAmHha-4OPWrrpmWd-E";
        private static TelegramBotClient? client;



        static async Task Main(string[] args)
        {
            Console.WriteLine("Початок");
            Console.WriteLine("========================================================");






            // Проверка наличия файла для чтения словаря
            Dictionary<long, string> userGroup = new Dictionary<long, string>();
            string filePath = "user_groups.txt";
            if (global::System.IO.File.Exists(filePath))
            {
                string[] fileContent = global::System.IO.File.ReadAllLines(filePath);
                for (int i = 0; i < fileContent.Length; i += 2)
                {
                    long chatId = long.Parse(fileContent[i]);
                    string groupValue = fileContent[i + 1];
                    userGroup.Add(chatId, groupValue);
                }
            }






            //старт телеграм бота
            client = new TelegramBotClient(token);
            client.StartReceiving(Update, Error);
            Console.ReadLine();


            var group = "c_hlnnlo9824qj9tc1ita1ofhgu8@group.calendar.google.com";
            var date = DateTime.Today;
            var events = await ScheduleParser.GetScheduleForGroup(group, date);

            if (events == null || events.Count == 0)
            {
                Console.WriteLine($"Не знайдено подій для групи {group} на дату {date.ToShortDateString()}");
                return;
            }



            async static Task Update(ITelegramBotClient botClient, Update update, CancellationToken token)
            {
                var groupNames = new List<string> { "ПІ-91", "ПІ-20", "ПІ-21", "ПІ-22", "КН-19", "КН-20", "КН-21", "КН-22", "ІН-19", "ІН-21", "ІН-22" };
                var message = update.Message;
                long chatId = message.Chat.Id;

                if (message.Type == MessageType.Text)
                {
                    if (message.Text == "/start")
                    {
                        if (userGroups.ContainsKey(chatId))
                        {
                            await botClient.SendTextMessageAsync(chatId, $"Ви обрали групу {userGroups[chatId]}.\nОберіть один з наступних пунктів:", replyMarkup: GetMainKeyboard());
                        }
                        else
                        {
                            await botClient.SendTextMessageAsync(chatId, "Виберіть свою групу:", replyMarkup: GetGroupKeyboard());
                        }
                    }
                    else if (userGroups.ContainsKey(chatId))
                    {
                        if (message.Text == "Вивести розклад на сьогодні")
                        {
                            var todaySchedule = await GetSchedule(chatId, DateTime.Today);
                            await botClient.SendTextMessageAsync(chatId, todaySchedule);
                        }
                        else if (message.Text == "Вивести розклад на завтра")
                        {
                            var tomorrowSchedule = await GetSchedule(chatId, DateTime.Today.AddDays(1));
                            await botClient.SendTextMessageAsync(chatId, tomorrowSchedule);
                        }
                        else if (message.Text == "Вивести розклад на наступні 5 днів")
                        {
                            var schedule = await GetScheduleForNextFiveDays(chatId);
                            await botClient.SendTextMessageAsync(chatId, schedule);
                        }
                        else if (message.Text == "Змінити групу")
                        {
                            userGroups.Remove(chatId);
                            await botClient.SendTextMessageAsync(chatId, "Виберіть свою групу:", replyMarkup: GetGroupKeyboard());
                        }
                    }
                    else if (groupNames.Contains(message.Text))
                    {
                        userGroups[chatId] = message.Text;
                        await botClient.SendTextMessageAsync(chatId, $"Ваша група: {message.Text}.\nОберіть один з наступних пунктів:", replyMarkup: GetMainKeyboard());
                    }
                }
            }
            static Task Error(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
            {
                Console.WriteLine($"Error: {exception.Message}");
                return Task.CompletedTask;
            }




            Console.WriteLine("Натисніть будь-яку кнопку, щоб продовжити...");
            Console.ReadLine();
        }
    }
}

