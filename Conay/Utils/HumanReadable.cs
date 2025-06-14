using System;

namespace Conay.Utils;

public static class HumanReadable
{
    public static string TimeAgo(DateTime dateTime)
    {
        TimeSpan timeSpan = DateTime.UtcNow.Subtract(dateTime);

        return timeSpan.TotalSeconds switch
        {
            <= 60 => $"{timeSpan.Seconds} seconds ago",

            _ => timeSpan.TotalMinutes switch
            {
                <= 1 => "about a minute ago",
                < 60 => $"{timeSpan.Minutes} minutes ago",
                _ => timeSpan.TotalHours switch
                {
                    <= 1 => "about an hour ago",
                    < 24 => $"{timeSpan.Hours} hours ago",
                    _ => timeSpan.TotalDays switch
                    {
                        <= 1 => "yesterday",
                        <= 30 => $"{timeSpan.Days} days ago",

                        <= 60 => "about a month ago",
                        < 365 => $"{timeSpan.Days / 30} months ago",

                        <= 365 * 2 => "about a year ago",
                        _ => $"{timeSpan.Days / 365} years ago"
                    }
                }
            }
        };
    }
}