using System.Windows;

namespace teamProject
{
    public class DateUK
    {
        public string Day { get; set; }
        public string DayOfMonth { get; set; }
        public string Month { get; set; }
        public string Year { get; set; }

        private static List<string> days = new List<string>
        {
            "Нд",
            "Пн",
            "Вт",
            "Ср",
            "Чт",
            "Пт",
            "Сб",
        };

        private static List<string> months = new List<string>()
        {
            "Січень",
            "Лютий",
            "Березень",
            "Квітень",
            "Травень",
            "Червень",
            "Липень",
            "Серпень",
            "Вересень",
            "Жовтень",
            "Листопад",
            "Грудень",
        };

        public DateUK(DateTime dateTime)
        {
            Day = ConvertDay(dateTime.DayOfWeek);
            DayOfMonth = dateTime.Day.ToString();
            Month = ConvertMonth(dateTime.Month);
            Year = dateTime.Year.ToString();
        }

        public static string ConvertDate(DateTime dateTime)
        {
            DayOfWeek dayOfWeek = dateTime.DayOfWeek;
            string dayOfWeekString = "";
            string dayDateSeparator = "";

            if (dayOfWeek != 0)
            {
                dayOfWeekString = ConvertDay(dayOfWeek);
                dayDateSeparator = ", ";
            }

            int day = dateTime.Day;
            int monthInt = dateTime.Month;
            string monthString = ConvertMonth(monthInt).ToLower();
            string monthDateString = ConvertMonthDate(monthInt, monthString);
            int year = dateTime.Year;

            string fullDate = $"{dayOfWeekString}{dayDateSeparator}{day} {monthDateString} {year}";

            return fullDate;
        }

        public static string ConvertMonthDate(int month, string monthString)
        {
            string monthDateString;
            string monthSubString;

            if (month == 2)
            {
                monthSubString = monthString.Substring(0, monthString.Length - 2);
                monthDateString = $"{monthSubString}ого";
            }
            else if (month == 11)
            {
                monthDateString = $"{monthString}а";
            }
            else
            {
                monthSubString = monthString.Substring(0, monthString.Length - 3);
                monthDateString = $"{monthSubString}ня";
            }

            return monthDateString;
        }

        public static string ConvertDay(DayOfWeek day)
        {
            return days[(int)day - 1];
        }

        public static string ConvertMonth(int month)
        {
            if (month <= 0 || month > 12)
            {
                throw new Exception($"Неможливо конвертувати значення типу int ({month - 1}) в DateUK.Month.");
            }

            return months[month - 1];
        }
    }
}