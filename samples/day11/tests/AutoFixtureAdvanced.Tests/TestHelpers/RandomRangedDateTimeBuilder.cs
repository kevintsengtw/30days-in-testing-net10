using System;
using System.Reflection;

namespace AutoFixtureAdvanced.Tests.TestHelpers;

/// <summary>
/// class RandomRangedDateTimeBuilder - 自訂的 DateTime 範圍建構器，可以指定特定屬性
/// </summary>
public class RandomRangedDateTimeBuilder : ISpecimenBuilder
{
    private readonly DateTime _minDate;
    private readonly DateTime _maxDate;
    private readonly HashSet<string> _targetProperties;

    /// <summary>
    /// 建構子
    /// </summary>
    /// <param name="minDate">最小日期</param>
    /// <param name="maxDate">最大日期</param>
    /// <param name="targetProperties">目標屬性名稱</param>
    /// <exception cref="ArgumentException">當參數不合法時拋出</exception>
    public RandomRangedDateTimeBuilder(DateTime minDate, DateTime maxDate, params string[] targetProperties)
    {
        if (minDate >= maxDate)
        {
            throw new ArgumentException("最小日期必須小於最大日期", nameof(minDate));
        }

        if (targetProperties == null || targetProperties.Length == 0)
        {
            throw new ArgumentException("必須指定至少一個目標屬性", nameof(targetProperties));
        }

        this._minDate = minDate;
        this._maxDate = maxDate;
        this._targetProperties = new HashSet<string>(targetProperties);
    }

    /// <summary>
    /// 建立物件
    /// </summary>
    /// <param name="request">請求</param>
    /// <param name="context">內容</param>
    /// <returns>建立的物件或 NoSpecimen</returns>
    public object Create(object request, ISpecimenContext context)
    {
        if (request is PropertyInfo propertyInfo &&
            this._targetProperties.Contains(propertyInfo.Name))
        {
            // 支援 DateTime 和 DateTime? 類型
            if (propertyInfo.PropertyType == typeof(DateTime) ||
                propertyInfo.PropertyType == typeof(DateTime?))
            {
                var range = this._maxDate - this._minDate;
                var randomTicks = (long)(Random.Shared.NextDouble() * range.Ticks);
                return this._minDate.AddTicks(randomTicks);
            }
        }

        return new NoSpecimen();
    }
}