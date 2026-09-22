using Xunit;

namespace BuffCore.Utilities.Tests
{
    public class DateRangeHelperTests
    {
        [Fact]
        public void CountDaysBetween_IsInclusiveOfBothEndpoints()
        {
            var start = new DateOnly(2026, 9, 7);  // Monday
            var end = new DateOnly(2026, 9, 9);    // Wednesday

            Assert.Equal(3, DateRangeHelper.CountDaysBetween(start, end));
        }

        [Fact]
        public void CountDaysBetween_SingleDayRangeCountsOne()
        {
            var day = new DateOnly(2026, 9, 22);

            Assert.Equal(1, DateRangeHelper.CountDaysBetween(day, day));
        }

        [Fact]
        public void CountDaysBetween_ThrowsWhenEndBeforeStart()
        {
            Assert.Throws<ArgumentException>(
                () => DateRangeHelper.CountDaysBetween(new DateOnly(2026, 9, 10), new DateOnly(2026, 9, 9)));
        }

        [Fact]
        public void CountDaysBetween_ExcludesSpecifiedDaysOfWeek()
        {
            var start = new DateOnly(2026, 9, 7);   // Monday
            var end = new DateOnly(2026, 9, 20);    // Sunday — 14 days total, 4 weekend days

            var weekendExclusions = new[] { DayOfWeek.Saturday, DayOfWeek.Sunday };

            Assert.Equal(10, DateRangeHelper.CountDaysBetween(start, end, weekendExclusions));
        }

        [Fact]
        public void CountDaysBetween_OverloadWithoutExclusionsMatchesEmptyArray()
        {
            var start = new DateOnly(2026, 9, 7);
            var end = new DateOnly(2026, 9, 20);

            Assert.Equal(
                DateRangeHelper.CountDaysBetween(start, end, []),
                DateRangeHelper.CountDaysBetween(start, end));
        }
    }
}
