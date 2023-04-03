﻿using Google.Apis.Auth.OAuth2;
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
            new KeyboardButton("Вивести розклад на поточний тиждень"),
            new KeyboardButton("Вивести розклад на наступний тиждень"),
        },
        new[]
        {
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
            string filePath = "C:\\Users\\Sasha\\Desktop\\TelegramBot\\user_Group.txt";
            if (global::System.IO.File.Exists(filePath))
            {
                string[] fileContent = global::System.IO.File.ReadAllLines(filePath);
                foreach (var line in fileContent)
                {
                    string[] parts = line.Split(':');
                    if (parts.Length == 2 && long.TryParse(parts[0], out long chatId))
                    {
                        string groupValue = parts[1];
                        userGroup.Add(chatId, groupValue);
                    }
                }
                foreach (var kvp in userGroup)
                {
                    Console.WriteLine($"ChatId: {kvp.Key}, GroupValue: {kvp.Value}");
                }
            }






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
                    if (userGroups.ContainsKey(chatId))
                    {
                        Console.WriteLine($"chatId {chatId} присутній у словнику груп користувачів");
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
                            var events = calendar.GetEvents("3f451441fca96853e1ccaa54e186242da835046cefa025a5bfba513b7d5d4986@group.calendar.google.com")
                                .Where(e => e.Group == groupName && e.StartTime >= DateTime.Today && e.StartTime < DateTime.Today.AddDays(7))
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

                                var messageToSend = $"Розклад для групи {groupName} на {DateTime.Today.ToShortDateString()}:\n";

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
                        else if (message.Text == "Змінити групу")
                        {
                            userGroups.Remove(chatId);
                            await botClient.SendTextMessageAsync(chatId, "Виберіть свою групу:", replyMarkup: GetGroupKeyboard());
                        }


                        //вивід пар на наступний тиждень
                        else if (message.Text == "Вивести розклад на наступний тиждень")
                        {
                            var groupName = userGroups[chatId];
                            var calendar = new GoogleCalendar(userGroups);
                            var events = calendar.GetEvents("3f451441fca96853e1ccaa54e186242da835046cefa025a5bfba513b7d5d4986@group.calendar.google.com")
                                .Where(e => e.Group == groupName && e.StartTime >= DateTime.Today.AddDays(7) && e.StartTime < DateTime.Today.AddDays(14))
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

                                var messageToSend = $"Розклад для групи {groupName} на {DateTime.Today.ToShortDateString()}:\n";

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
                        else if (message.Text == "Змінити групу")
                        {
                            userGroups.Remove(chatId);
                            await botClient.SendTextMessageAsync(chatId, "Виберіть свою групу:", replyMarkup: GetGroupKeyboard());
                        }
                    }
                    else
                    {
                        Console.WriteLine($"chatId {chatId} відсутній у словнику userGroups»");
                    }
                    if (message.Text != null && groupNames.Contains(message.Text))
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
            AppDomain.CurrentDomain.ProcessExit += (s, e) => SaveDictionaryToFile(userGroups, filePath);
            Console.ReadLine();
            }
        }
    }
