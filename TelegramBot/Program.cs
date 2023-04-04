using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Threading;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using System.IO;
using System.Data.Common;
using System.Text;
using Newtonsoft.Json.Linq;
using Google.Apis.Util.Store;
using System.Globalization;
using static Google.Apis.Calendar.v3.Data.ConferenceData;
using Newtonsoft.Json;
using System.IO;
using static GoogleCalendarApi.Program;

namespace GoogleCalendarApi
{
    class Program
    {
        //токен для телеграм бота
        private static string token { get; set; } = "5522982120:AAFwClh10_pQJyvAvAmHha-4OPWrrpmWd-E";
        private static TelegramBotClient? client;

        // Словарь для хранения выбранной пользователем группы и его chat id
        static Dictionary<long, string> userGroups = new Dictionary<long, string>();

        // Словарь для хранения выбранной пользователем оповещания да или нет и его chat id
        public static Dictionary<long, bool> subscribers = new Dictionary<long, bool>();




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
            new KeyboardButton("Вивести розклад на поточний тиждень"),
            new KeyboardButton("Вивести розклад на наступний тиждень"),
        },
        new[]
        {
            new KeyboardButton("Отримувати сповіщення до початку пари"),
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


        //метод для створення клавіатур чи отримувати сповіщення про початок подіїї
        private static ReplyKeyboardMarkup YesNO()
        {
            var keyboard = new ReplyKeyboardMarkup(new[]
            {
        new[]
        {
            new KeyboardButton("Так"),
            new KeyboardButton("Ні"),
        },
        new[]
        {
            new KeyboardButton("Повернутися назад"),
        },
    });
            return keyboard;
        }


        //запис даних про подію
        public class CalendarEvent
        {
            public string? Group { get; set; }
            public string? Subject { get; set; }
            public string? Teacher { get; set; }
            public string? GoogleMeetLink { get; set; }
            public DateTime StartTime { get; set; }
            public DateTime EndTime { get; set; }

            public override string ToString()
            {
                return $"{Group}, {Subject}, {Teacher}, {GoogleMeetLink},{StartTime}, {EndTime}";
            }
        }

        public class GoogleCalendar
        {
            private readonly string[] Scopes = { CalendarService.Scope.CalendarReadonly };
            private readonly string ApplicationName = "Google Calendar API";
            private readonly Dictionary<long, string> UserGroups;

            public GoogleCalendar(Dictionary<long, string> userGroups)
            {
                UserGroups = userGroups;
            }

            public List<CalendarEvent> GetEvents(string calendarId)
            {
                var service = CreateCalendarService();
                var events = GetEventsFromCalendar(service, calendarId);
                var calendarEvents = MapEventsToCalendarEvents(events);

                return calendarEvents;
            }

            private CalendarService CreateCalendarService()
            {
                try
                {
                    GoogleCredential credential;
                    using (var stream = new FileStream("C:\\Users\\Sasha\\Desktop\\TelegramBot/telegrambot-363818-c5afa8c735d4.json", FileMode.Open, FileAccess.Read))
                    {
                        credential = GoogleCredential.FromStream(stream)
                            .CreateScoped(Scopes);
                    }

                    var service = new CalendarService(new BaseClientService.Initializer()
                    {
                        HttpClientInitializer = credential,
                        ApplicationName = ApplicationName,
                    });

                    return service;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An error of type {ex.GetType()} occurred: {ex}");
                    throw;
                }
            }

            private Events GetEventsFromCalendar(CalendarService service, string calendarId)
            {
                EventsResource.ListRequest request = service.Events.List(calendarId);
                request.TimeMin = DateTime.Now;
                request.ShowDeleted = false;
                request.SingleEvents = true;
                request.MaxResults = 100;
                request.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;

                Events events = request.Execute();
                return events;
            }

            private List<CalendarEvent> MapEventsToCalendarEvents(Events events)
            {
                var calendarEvents = new List<CalendarEvent>();

                if (events.Items != null && events.Items.Count > 0)
                {
                    foreach (var eventItem in events.Items)
                    {
                        if (!string.IsNullOrEmpty(eventItem.Summary))
                        {
                            var summaryParts = eventItem.Summary.Split(',');
                            if (summaryParts.Length >= 3)
                            {
                                var groups = summaryParts.Take(summaryParts.Length - 2).ToList();
                                var subject = summaryParts[summaryParts.Length - 2].Trim();
                                var teacher = summaryParts[summaryParts.Length - 1].Trim();

                                foreach (var group in groups)
                                {
                                    if (UserGroups.ContainsValue(group))
                                    {
                                        var start = eventItem.Start.DateTime ?? DateTime.MinValue;
                                        var end = eventItem.End.DateTime ?? DateTime.MinValue;

                                        var googleMeetLink = "";
                                        if (eventItem.ConferenceData != null && eventItem.ConferenceData.CreateRequest != null)
                                        {
                                            googleMeetLink = eventItem.ConferenceData.CreateRequest.ConferenceSolutionKey.Type == "hangoutsMeet" ? eventItem.ConferenceData.EntryPoints[0].Uri : "";
                                        }
                                        if (eventItem.ConferenceData != null && eventItem.ConferenceData.EntryPoints != null && eventItem.ConferenceData.EntryPoints.Any(ep => ep.Uri.StartsWith("https://meet.google.com/")))
                                        {
                                            googleMeetLink = eventItem.ConferenceData.EntryPoints.First(ep => ep.Uri.StartsWith("https://meet.google.com/")).Uri;
                                        }

                                        var calendarEvent = new CalendarEvent
                                        {
                                            Group = group,
                                            Subject = subject,
                                            Teacher = teacher,
                                            GoogleMeetLink = googleMeetLink,
                                            StartTime = start,
                                            EndTime = end
                                        };

                                        calendarEvents.Add(calendarEvent);
                                    }
                                    else
                                    {
                                        Console.WriteLine($"Подія з підсумком '{eventItem.Summary}' не належить до жодної групи користувачів.");
                                    }
                                }
                            }
                            else
                            {
                                Console.WriteLine($"Подія з підсумком '{eventItem.Summary}' не має достатньо інформації.");
                            }
                        }
                    }
                }

                return calendarEvents;
            }

        }



        //збереження в файл JSON
        private static void SaveUserGroupToFile(long chatId, string groupName)
        {
            string filePath = "C:\\Users\\Sasha\\Desktop\\TelegramBot\\user_Group.json";
            Dictionary<long, string> userGroups;

            if (System.IO.File.Exists(filePath))
            {
                string jsons = System.IO.File.ReadAllText(filePath);
                if (!string.IsNullOrEmpty(jsons))
                {
                    userGroups = JsonConvert.DeserializeObject<Dictionary<long, string>>(jsons);
                }
                else
                {
                    userGroups = new Dictionary<long, string>();
                }
            }
            else
            {
                userGroups = new Dictionary<long, string>();
            }

            if (userGroups.ContainsKey(chatId))
            {
                userGroups[chatId] = groupName;
            }
            else
            {
                userGroups.Add(chatId, groupName);
            }

            string json = JsonConvert.SerializeObject(userGroups);
            System.IO.File.WriteAllText(filePath, json);
        }





        private static void SaveSubscribersToFile()
        {
            string filePath = "C:\\Users\\Sasha\\Desktop\\TelegramBot\\YesNO.json";
            Dictionary<long, bool> reminders;

            if (System.IO.File.Exists(filePath))
            {
                string jsons = System.IO.File.ReadAllText(filePath);
                if (!string.IsNullOrEmpty(jsons))
                {
                    reminders = JsonConvert.DeserializeObject<Dictionary<long, bool>>(jsons);
                }
                else
                {
                    reminders = new Dictionary<long, bool>();
                }
            }
            else
            {
                reminders = new Dictionary<long, bool>();
            }

            foreach (var subscriber in subscribers)
            {
                reminders[subscriber.Key] = subscriber.Value;
            }

            string json = JsonConvert.SerializeObject(reminders);
            System.IO.File.WriteAllText(filePath, json);
        }









        public class Subscriber
        {
            public long ChatId { get; set; }
            public bool Reminder { get; set; }

            public Subscriber(long chatId, bool reminder)
            {
                ChatId = chatId;
                Reminder = reminder;
            }
        }


        //перевірка подій на 24 години і сповіщення про них за 30 хвилин до початку
            public async Task CheckEvents()
            {
                var remindersSent = new Dictionary<int, bool>();
                var calendar = new GoogleCalendar(userGroups);
                var events = calendar.GetEvents("3f451441fca96853e1ccaa54e186242da835046cefa025a5bfba513b7d5d4986@group.calendar.google.com")
        .Where(e => e.StartTime >= DateTime.Now && e.StartTime <= DateTime.Now.AddHours(24))
                    .ToList();

                foreach (var e in events)
                {
                    foreach (var subscriber in subscribers.Select(s => new Subscriber(s.Key, s.Value)))
                    {
                        // Перевірка, чи хоче користувач отримувати нагадування
                        if (subscriber.Reminder)
                        {
                            var reminderTime = e.StartTime;
                            var timeToEvent = reminderTime - DateTime.Now;

                            // Перевірка, чи час нагадування в майбутньому
                            if (timeToEvent.TotalMinutes > 0)
                            {
                                if (timeToEvent.TotalMinutes <= 30)
                                {
                                    // Створення ключа для словника remindersSent
                                    var key = $"{e.Subject}-{e.StartTime.Date.ToString()}";

                                    // Перевірка, чи було вже виведено сповіщення для цієї пари
                                    if (!remindersSent.ContainsKey(key.GetHashCode()))
                                    {

                                        // Send reminder message to subscriber
                                        await client.SendTextMessageAsync(subscriber.ChatId, $"Нагадування: скоро почток наступної пари! \n\nНазва пари: {e.Subject} \nПочаток: {e.StartTime.ToShortTimeString()} - кінець: {e.EndTime.ToShortTimeString()} ,\nВикладач: {e.Teacher}\nСилка на пару: {(!string.IsNullOrEmpty(e.GoogleMeetLink) ? e.GoogleMeetLink : "Силка на пару відсутня")}\n\n");

                                        // Встановлення значення флага для ключа в словнику remindersSent
                                        remindersSent[key.GetHashCode()] = true;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        







        static async Task Main(string[] args)
        {
            Console.WriteLine("Початок");
            Console.WriteLine("========================================================");






            // Зчитування словника з файлу JSON
            string filePath = "C:\\Users\\Sasha\\Desktop\\TelegramBot\\user_Group.json";
            
            if (System.IO.File.Exists(filePath))
            {
                string jsons = System.IO.File.ReadAllText(filePath);
                if (!string.IsNullOrEmpty(jsons))
                {
                    userGroups = JsonConvert.DeserializeObject<Dictionary<long, string>>(jsons);
                }
                else
                {
                    userGroups = new Dictionary<long, string>();
                }
            }
            else
            {
                userGroups = new Dictionary<long, string>();
            }



            string filePath2 = "C:\\Users\\Sasha\\Desktop\\TelegramBot\\YesNO.json";

            // Зчитуємо існуючий файл, якщо він існує
            if (System.IO.File.Exists(filePath2))
            {
                string jsons = System.IO.File.ReadAllText(filePath2);
                if (!string.IsNullOrEmpty(jsons))
                {
                    subscribers = JsonConvert.DeserializeObject<Dictionary<long, bool>>(jsons);
                }
            }

            // Додаємо нові дані до словника
            foreach (KeyValuePair<long, bool> subscriber in Program.subscribers)
            {
                subscribers[subscriber.Key] = subscriber.Value;
            }

            // Записуємо оновлений словник у файл
            string json = JsonConvert.SerializeObject(subscribers);
            System.IO.File.WriteAllText(filePath2, json);







            //старт телеграм бота
            client = new TelegramBotClient(token);
                client.StartReceiving(Update, Error);




                async static Task Update(ITelegramBotClient botClient, Update update, CancellationToken token)
                {
                    var groupNames = new List<string> { "ПІ-91", "ПІ-20", "ПІ-21", "ПІ-22", "КН-19", "КН-20", "КН-21", "КН-22", "ІН-19", "ІН-21", "ІН-22" };
                    if (update == null)
                        return;

                    var message = update.Message;

                    if (message == null)
                        return;


                long chatId = message.Chat.Id;

                if (!userGroups.TryGetValue(chatId, out string group))
                {
                    userGroups[chatId] = "Group 1"; // Додати новий ключ зі значенням за замовчуванням
                    group = "Group 1";
                }


                if (message.Type == MessageType.Text)
                {
                    if (message.Text == "/start")
                    {
                        if (userGroups.TryGetValue(chatId, out string groupValue2) && groupValue2 != "Group 1")
                        {

                            await botClient.SendTextMessageAsync(chatId, $"Ви обрали групу {userGroups[chatId]}.\nОберіть один з наступних пунктів:", replyMarkup: GetMainKeyboard());
                        }
                        if (userGroups.TryGetValue(chatId, out string groupValue) && groupValue == "Group 1")
                        {
                            await botClient.SendTextMessageAsync(chatId, "Виберіть свою групу:", replyMarkup: GetGroupKeyboard());
                        }
                    }


                        //вивід пар на сьогодні
                        if (message.Text == "Вивести розклад на сьогодні")
                        {
                            var groupName = userGroups[chatId];
                            var calendar = new GoogleCalendar(userGroups);
                            var events = calendar.GetEvents("3f451441fca96853e1ccaa54e186242da835046cefa025a5bfba513b7d5d4986@group.calendar.google.com")
                                .Where(e => e.Group == groupName && e.StartTime.Date == DateTime.Today)
                                .OrderBy(e => e.StartTime);

                            if (events.Any())
                            {
                                var messageToSend = $"Розклад для групи {groupName} на {DateTime.Today.ToShortDateString()}:\n";

                                foreach (var ev in events)
                                {
                                    messageToSend += $"Назва пари: {ev.Subject} \nПочаток: {ev.StartTime.ToShortTimeString()} - кінець: {ev.EndTime.ToShortTimeString()} ,\nВикладач: {ev.Teacher}\nСилка на пару: {(!string.IsNullOrEmpty(ev.GoogleMeetLink) ? ev.GoogleMeetLink : "Силка на пару відсутня")}\n\n";
                                }

                                await botClient.SendTextMessageAsync(chatId, messageToSend);
                            }
                            else
                            {
                                await botClient.SendTextMessageAsync(chatId, $"На сьогодні для групи {groupName} немає подій у календарі.");
                            }
                        }


                        //вивід пар на завтра
                        else if (message.Text == "Вивести розклад на завтра")
                        {
                            var groupName = userGroups[chatId];
                            var calendar = new GoogleCalendar(userGroups);
                            var events = calendar.GetEvents("3f451441fca96853e1ccaa54e186242da835046cefa025a5bfba513b7d5d4986@group.calendar.google.com")
                                .Where(e => e.Group == groupName && e.StartTime.Date == DateTime.Today.AddDays(1))
                                .OrderBy(e => e.StartTime);

                            if (userGroups.ContainsKey(chatId))
                            {
                                if (events.Any())
                                {
                                    var messageToSend = $"Розклад для групи {groupName} на {DateTime.Today.ToShortDateString()}:\n";

                                    foreach (var ev in events)
                                    {
                                        messageToSend += $"Назва пари: {ev.Subject} \nПочаток: {ev.StartTime.ToShortTimeString()} - кінець: {ev.EndTime.ToShortTimeString()} ,\nВикладач: {ev.Teacher}\nСилка на пару: {(!string.IsNullOrEmpty(ev.GoogleMeetLink) ? ev.GoogleMeetLink : "Силка на пару відсутня")}\n\n";
                                    }

                                    await botClient.SendTextMessageAsync(chatId, messageToSend);
                                }
                                else
                                {
                                    await botClient.SendTextMessageAsync(chatId, $"На завтра для групи {groupName} немає подій у календарі.");
                                }
                            }
                            else
                            {
                                await botClient.SendTextMessageAsync(chatId, "Ви не вибрали групу виберіть х:", replyMarkup: GetGroupKeyboard());
                            }
                        }


                        //вивід пар поточний тиждень
                        else if (message.Text == "Вивести розклад на поточний тиждень")
                        {
                            var groupName = userGroups[chatId];
                            var calendar = new GoogleCalendar(userGroups);
                            var today = DateTime.Today;
                            var startOfWeek = today.AddDays(-(int)today.DayOfWeek + (int)DayOfWeek.Monday);
                            var endOfWeek = startOfWeek.AddDays(6);
                            var events = calendar.GetEvents("3f451441fca96853e1ccaa54e186242da835046cefa025a5bfba513b7d5d4986@group.calendar.google.com")
                            .Where(e => e.Group == groupName && e.StartTime >= startOfWeek && e.StartTime <= endOfWeek)
                            .OrderBy(e => e.StartTime);

                            if (events.Any())
                            {
                                var scheduleByDay = new Dictionary<DayOfWeek, List<string>>();
                                scheduleByDay[DayOfWeek.Monday] = new List<string>();
                                scheduleByDay[DayOfWeek.Tuesday] = new List<string>();
                                scheduleByDay[DayOfWeek.Wednesday] = new List<string>();
                                scheduleByDay[DayOfWeek.Thursday] = new List<string>();
                                scheduleByDay[DayOfWeek.Friday] = new List<string>();
                                scheduleByDay[DayOfWeek.Saturday] = new List<string>();
                                scheduleByDay[DayOfWeek.Sunday] = new List<string>();

                                foreach (var ev in events)
                                {
                                    var dayOfWeek = ev.StartTime.DayOfWeek;
                                    var formattedEvent = $"Назва пари: {ev.Subject} \nПочаток: {ev.StartTime.ToShortTimeString()} - кінець: {ev.EndTime.ToShortTimeString()} ,\nВикладач: {ev.Teacher}\nСилка на пару: {(!string.IsNullOrEmpty(ev.GoogleMeetLink) ? ev.GoogleMeetLink : "Силка на пару відсутня")}\n";

                                    scheduleByDay[dayOfWeek].Add(formattedEvent);
                                }

                                var messageToSend = $"Розклад для групи {groupName} на поточний тиждень \nТобто з понеділка {startOfWeek.ToShortDateString()} по неділю {endOfWeek.ToShortDateString()}\n";

                                foreach (var pair in scheduleByDay)
                                {
                                    if (pair.Value.Any())
                                    {
                                        var dayOfWeekName = CultureInfo.CurrentCulture.DateTimeFormat.GetDayName(pair.Key);
                                        var eventsForDay = string.Join("\n", pair.Value);

                                        messageToSend += $"\n{dayOfWeekName}:\n{eventsForDay}\n";
                                    }
                                }

                                await botClient.SendTextMessageAsync(chatId, messageToSend);
                            }
                            else
                            {
                                await botClient.SendTextMessageAsync(chatId, $"На поточний тиждень для групи {groupName} немає подій у календарі.");
                            }
                        }


                        //вивід пар на наступний тиждень
                            else if (message.Text == "Вивести розклад на наступний тиждень")
                            {
                                var groupName = userGroups[chatId];
                                var calendar = new GoogleCalendar(userGroups);
                                var nextMonday = DateTime.Today.AddDays(((int)DayOfWeek.Monday - (int)DateTime.Today.DayOfWeek + 7) % 7);
                                var nextSunday = nextMonday.AddDays(6);
                                var events = calendar.GetEvents("3f451441fca96853e1ccaa54e186242da835046cefa025a5bfba513b7d5d4986@group.calendar.google.com")
                                .Where(e => e.Group == groupName && e.StartTime >= nextMonday && e.StartTime <= nextSunday)
                                .OrderBy(e => e.StartTime);


                            if (events.Any())
                                {
                                    var scheduleByDay = new Dictionary<DayOfWeek, List<string>>();
                                    scheduleByDay[DayOfWeek.Monday] = new List<string>();
                                    scheduleByDay[DayOfWeek.Tuesday] = new List<string>();
                                    scheduleByDay[DayOfWeek.Wednesday] = new List<string>();
                                    scheduleByDay[DayOfWeek.Thursday] = new List<string>();
                                    scheduleByDay[DayOfWeek.Friday] = new List<string>();
                                    scheduleByDay[DayOfWeek.Saturday] = new List<string>();
                                    scheduleByDay[DayOfWeek.Sunday] = new List<string>();

                                    foreach (var ev in events)
                                    {
                                        var dayOfWeek = ev.StartTime.DayOfWeek;
                                        var formattedEvent = $"Назва пари: {ev.Subject} \nПочаток: {ev.StartTime.ToShortTimeString()} - кінець: {ev.EndTime.ToShortTimeString()} ,\nВикладач: {ev.Teacher}\nСилка на пару: {(!string.IsNullOrEmpty(ev.GoogleMeetLink) ? ev.GoogleMeetLink : "Силка на пару відсутня")}\n";

                                        scheduleByDay[dayOfWeek].Add(formattedEvent);
                                    }

                                    var messageToSend = $"Розклад для групи {groupName} на наступний тиждень \nТобто з понеділка {nextMonday.ToShortDateString()} по неділю {nextSunday.ToShortDateString()}:\n";

                                    foreach (var pair in scheduleByDay)
                                    {
                                        if (pair.Value.Any())
                                        {
                                            var dayOfWeekName = CultureInfo.CurrentCulture.DateTimeFormat.GetDayName(pair.Key);
                                            var eventsForDay = string.Join("\n", pair.Value);

                                            messageToSend += $"\n{dayOfWeekName}:\n{eventsForDay}\n";
                                        }
                                    }

                                    await botClient.SendTextMessageAsync(chatId, messageToSend);
                                }
                                else
                                {
                                    await botClient.SendTextMessageAsync(chatId, $"На наступний тиждень для групи {groupName} немає подій у календарі.");
                                }
                        }

                        if (message.Text == "Отримувати сповіщення до початку пари")
                        {
                            chatId = message.Chat.Id;
                            var groupName = userGroups[chatId];

                            if (Program.subscribers.ContainsKey(chatId))
                            {
                                var isSubscribed = Program.subscribers[chatId];
                                await botClient.SendTextMessageAsync(chatId, $"Ви вже маєте підписку на сповіщення: {isSubscribed}");
                                // Отримуємо об'єкт клавіатури з callback_data для кнопок "Так" і "Ні"
                                var keyboard = YesNO();
                                // Відправляємо запитання про включення сповіщень і відправляємо клавіатуру з кнопками "Так" і "Ні"
                                await botClient.SendTextMessageAsync(chatId, "Включити сповіщення?", replyMarkup: keyboard);
                            }
                            else
                            {
                                // Отримуємо об'єкт клавіатури з callback_data для кнопок "Так" і "Ні"
                                var keyboard = YesNO();
                                // Відправляємо запитання про включення сповіщень і відправляємо клавіатуру з кнопками "Так" і "Ні"
                                await botClient.SendTextMessageAsync(chatId, "Включити сповіщення?", replyMarkup: keyboard);
                            }
                        }
                        else if (message.Text != null)
                        {
                            chatId = message.Chat.Id;
                            var groupName = userGroups[chatId];
                            // Якщо натиснута кнопка "Так"
                            if (message.Text == "Так")
                            {
                                groupName = userGroups[chatId];
                                Program.subscribers[chatId] = true;
                                await botClient.SendTextMessageAsync(chatId, $"Сповіщення включено: {Program.subscribers[chatId]}");
                                SaveSubscribersToFile();
                            }
                            // Якщо натиснута кнопка "Ні"
                            if (message.Text == "Ні")
                            {
                                groupName = userGroups[chatId];
                                Program.subscribers[chatId] = false;
                                await botClient.SendTextMessageAsync(chatId, $"Сповіщення включено: {Program.subscribers[chatId]}");
                                SaveSubscribersToFile();
                            }
                        }
                        if (message.Text == "Повернутися назад")
                        {
                            var groupName = userGroups[chatId];
                            await botClient.SendTextMessageAsync(chatId, $"Ви обрали групу {userGroups[chatId]}.\nОберіть один з наступних пунктів:", replyMarkup: GetMainKeyboard());
                        }


                        else if (message.Text == "Змінити групу")
                        {
                            userGroups.Remove(chatId);
                            await botClient.SendTextMessageAsync(chatId, "Виберіть свою групу:", replyMarkup: GetGroupKeyboard());
                        }
                    if (message.Text != null && groupNames.Contains(message.Text))
                    {
                        if (userGroups.ContainsKey(chatId))
                        {
                            userGroups[chatId] = message.Text;
                        }
                        else
                        {
                            userGroups.Add(chatId, message.Text);
                        }
                        await botClient.SendTextMessageAsync(chatId, $"Ваша група: {message.Text}.\nОберіть один з наступних пунктів:", replyMarkup: GetMainKeyboard());
                    }
                }
                }
                static Task Error(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
                {
                    Console.WriteLine($"Error: {exception.Message}");
                    return Task.CompletedTask;
            }

            Program program = new Program();
            bool keepRunning = true;
            while (keepRunning)
            {
                program.CheckEvents();
                Thread.Sleep(TimeSpan.FromSeconds(30));

                // перевірка на наявність команди для зупинки
                if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape)
                {
                    keepRunning = false;
                }
            }

            Console.WriteLine("Цикл зупинений");

            Console.ReadLine();
            Console.WriteLine("Натисніть будь-яку кнопку, щоб продовжити...");
            foreach (KeyValuePair<long, string> userGroup in userGroups)
            {
                SaveUserGroupToFile(userGroup.Key, userGroup.Value);
            }



            Console.ReadLine();
            }
        }
    }

