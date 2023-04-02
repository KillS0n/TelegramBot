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
        //токен для телеграм бота
        private static string token { get; set; } = "5522982120:AAFwClh10_pQJyvAvAmHha-4OPWrrpmWd-E";
        private static TelegramBotClient? client;

        // Словарь для хранения выбранной пользователем группы и его chat id
        static Dictionary<long, string> userGroups = new Dictionary<long, string>();




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






        //клас в якому відбувається авторизація і отримання даних з гугл календаря
        public static class ScheduleParser
        {
            private static string[] Scopes = { CalendarService.Scope.CalendarReadonly };
            private static CalendarService? Service = null;

            //получення даних для авторизації GoogleCalendarService
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


            //шукає календарь і видає список подій
            public static async Task<List<Event>> GetScheduleForGroup(string group, DateTime date)
            {
                var calendarId = "c_hlnnlo9824qj9tc1ita1ofhgu8@group.calendar.google.com";
                var selectedDate = date; // використовуйте параметр методу

                var schedule = await ScheduleParser.GetScheduleForGroup(calendarId, selectedDate);
                if (schedule == null || schedule.Count == 0)
                {
                    return null; // повернути порожній список, щоб відобразити відповідне повідомлення
                }
                else
                {
                    var scheduleStr = ScheduleFormatter.FormatSchedule(schedule, selectedDate);
                    // виведення розкладу у вигляді рядка, наприклад:
                    Console.WriteLine(scheduleStr);
                    return schedule;
                }
            }
        }


        //клас в якому відбувається конвертація отриманих даних з календаря в рядок
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


        //запис даних про подію
        public class Event
        {
            public DateTime StartTime { get; set; }
            public DateTime EndTime { get; set; }
            public string? Subject { get; set; }
            public string? Teacher { get; set; }
            public string? Room { get; set; }
        }


        //беручи chatId і  групу генерує розклад
        private static async Task<string> GetSchedule(long chatId, DateTime date)
        {
            var group = userGroups[chatId];
            var schedule = await ScheduleParser.GetScheduleForGroup(group, date);
            if (schedule == null)
            {
                return "На цю дату розклад не знайдено для групи " + group;
            }
            var scheduleStr = ScheduleFormatter.FormatSchedule(schedule, date);
            return scheduleStr;
        }


        //беручи chatId і  групу генерує розклад на 5 днів
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


        //запис словника в файл
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
                            var groupName = userGroups[chatId];
                            var todaySchedule = await GetSchedule(chatId, DateTime.Today);
                            await botClient.SendTextMessageAsync(chatId, todaySchedule);
                        }
                        else if (message.Text == "Вивести розклад на завтра")
                        {
                            var groupName = userGroups[chatId];
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



            SaveDictionaryToFile(userGroups, filePath);
            Console.WriteLine("Натисніть будь-яку кнопку, щоб продовжити...");
            Console.ReadLine();
        }
    }
}
