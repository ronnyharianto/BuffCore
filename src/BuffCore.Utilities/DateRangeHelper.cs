namespace BuffCore.Utilities
{
    /// <summary>
    /// Helper methods for date range calculations.
    /// </summary>
    public static class DateRangeHelper
    {
        /// <summary>
        /// Counts the days between two dates, inclusive of both endpoints.
        /// </summary>
        /// <param name="startDate">The first day of the range.</param>
        /// <param name="endDate">The last day of the range.</param>
        /// <returns>The number of days in the inclusive range.</returns>
        public static int CountDaysBetween(DateOnly startDate, DateOnly endDate)
            => CountDaysBetween(startDate, endDate, []);

        /// <summary>
        /// Counts the days between two dates, inclusive of both endpoints,
        /// excluding occurrences of the specified days of week.
        /// </summary>
        /// <param name="startDate">The first day of the range.</param>
        /// <param name="endDate">The last day of the range.</param>
        /// <param name="excludedDaysOfWeek">Days of week to exclude from the count.</param>
        /// <returns>The number of days in the inclusive range, excluding the given days.</returns>
        public static int CountDaysBetween(DateOnly startDate, DateOnly endDate, DayOfWeek[] excludedDaysOfWeek)
        {
            if (endDate < startDate)
                throw new ArgumentException("End date cannot be earlier than start date.");

            int count = 0;
            for (var date = startDate; date <= endDate; date = date.AddDays(1))
            {
                if (!excludedDaysOfWeek.Contains(date.DayOfWeek))
                    count++;
            }

            return count;
        }
    }
}
