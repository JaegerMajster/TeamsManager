using System;
using System.Text.Json;
using FluentAssertions;
using TeamsManager.Core.Enums;
using TeamsManager.Core.Models;
using Xunit;

namespace TeamsManager.Tests.Models
{
    public class ApplicationSettingTests
    {
        // ... (istniejące testy dla WhenCreated, WhenSettingProperties, GetStringValue, GetIntValue, GetBoolValue, GetDateTimeValue, GetDecimalValue, GetJsonValue - pozostają bez zmian) ...

        // Istniejące testy dla GetXValue() (pozostają bez zmian)
        [Fact]
        public void GetStringValue_ShouldReturnValue()
        {
            var setting = new ApplicationSetting { Value = "TestValue" };
            setting.GetStringValue().Should().Be("TestValue");
        }

        [Theory]
        [InlineData("123", 123)]
        [InlineData("0", 0)]
        [InlineData("-5", -5)]
        [InlineData("abc", 0)]
        [InlineData("", 0)]
        public void GetIntValue_ShouldConvertOrReturnDefault(string value, int expected)
        {
            var setting = new ApplicationSetting { Value = value, Type = SettingType.Integer };
            setting.GetIntValue().Should().Be(expected);
        }

        [Theory]
        [InlineData("true", true)]
        [InlineData("TRUE", true)]
        [InlineData("false", false)]
        [InlineData("FALSE", false)]
        [InlineData("abc", false)]
        [InlineData("", false)]
        public void GetBoolValue_ShouldConvertOrReturnDefault(string value, bool expected)
        {
            var setting = new ApplicationSetting { Value = value, Type = SettingType.Boolean };
            setting.GetBoolValue().Should().Be(expected);
        }

        [Fact]
        public void GetDateTimeValue_ShouldConvertOrReturnNull()
        {
            var now = DateTime.UtcNow;
            var settingValid = new ApplicationSetting { Value = now.ToString("yyyy-MM-dd HH:mm:ss"), Type = SettingType.DateTime };

            // Pobierz wartość raz do zmiennej
            DateTime? dateTimeValue = settingValid.GetDateTimeValue();

            // Sprawdź, czy ma wartość
            dateTimeValue.Should().HaveValue("because a valid date string should be parsed successfully");

            // Teraz bezpiecznie używaj .Value
            dateTimeValue.Value.Year.Should().Be(now.Year);
            dateTimeValue.Value.Month.Should().Be(now.Month);
            dateTimeValue.Value.Day.Should().Be(now.Day);
            dateTimeValue.Value.Hour.Should().Be(now.Hour);
            dateTimeValue.Value.Minute.Should().Be(now.Minute);
            dateTimeValue.Value.Second.Should().Be(now.Second);

            var settingInvalid = new ApplicationSetting { Value = "not a date", Type = SettingType.DateTime };
            settingInvalid.GetDateTimeValue().Should().BeNull();
        }

        [Theory]
        [InlineData("123.45", 123.45)]
        [InlineData("0.0", 0.0)]
        [InlineData("-5.5", -5.5)]
        [InlineData("abc", 0.0)]
        [InlineData("", 0.0)]
        public void GetDecimalValue_ShouldConvertOrReturnDefault(string value, decimal expected)
        {
            var setting = new ApplicationSetting { Value = value, Type = SettingType.Decimal };
            setting.GetDecimalValue().Should().Be(expected);
        }

        private class JsonTestData { public string PropA { get; set; } = string.Empty; public int PropB { get; set; } }

        [Fact]
        public void GetJsonValue_ShouldDeserializeCorrectly()
        {
            var data = new JsonTestData { PropA = "Test", PropB = 10 };
            var jsonString = JsonSerializer.Serialize(data);
            var setting = new ApplicationSetting { Value = jsonString, Type = SettingType.Json };

            var deserialized = setting.GetJsonValue<JsonTestData>();
            deserialized.Should().NotBeNull();
            deserialized!.PropA.Should().Be("Test"); // Użycie ! dla pewności po NotBeNull
            deserialized.PropB.Should().Be(10);

            var settingInvalidJson = new ApplicationSetting { Value = "{invalid_json", Type = SettingType.Json };
            settingInvalidJson.GetJsonValue<JsonTestData>().Should().BeNull();
        }

        // ===== NOWE I UZUPEŁNIONE TESTY DLA SETVALUE =====
        [Fact]
        public void SetValue_String_ShouldSetValueAndType()
        {
            var setting = new ApplicationSetting();
            setting.SetValue("Test String");
            setting.Value.Should().Be("Test String");
            setting.Type.Should().Be(SettingType.String);
        }

        [Fact]
        public void SetValue_Int_ShouldSetValueAndType()
        {
            var setting = new ApplicationSetting();
            setting.SetValue(456);
            setting.Value.Should().Be("456");
            setting.Type.Should().Be(SettingType.Integer);
        }

        [Fact]
        public void SetValue_Bool_ShouldSetValueAndType()
        {
            var setting = new ApplicationSetting();
            setting.SetValue(true);
            setting.Value.Should().Be("true"); // Metoda SetValue(bool) zapisuje "true" lub "false"
            setting.Type.Should().Be(SettingType.Boolean);

            setting.SetValue(false);
            setting.Value.Should().Be("false");
            setting.Type.Should().Be(SettingType.Boolean);
        }

        [Fact]
        public void SetValue_DateTime_ShouldSetValueAndType()
        {
            var setting = new ApplicationSetting();
            var dateTimeValue = new DateTime(2023, 10, 26, 14, 30, 15, DateTimeKind.Utc);
            // Usuwamy milisekundy, bo ToString("yyyy-MM-dd HH:mm:ss") ich nie uwzględnia
            var expectedDateTimeString = new DateTime(dateTimeValue.Year, dateTimeValue.Month, dateTimeValue.Day,
                                                      dateTimeValue.Hour, dateTimeValue.Minute, dateTimeValue.Second, DateTimeKind.Utc)
                                         .ToString("yyyy-MM-dd HH:mm:ss");

            setting.SetValue(dateTimeValue);
            setting.Value.Should().Be(expectedDateTimeString);
            setting.Type.Should().Be(SettingType.DateTime);
        }

        [Fact]
        public void SetValue_Decimal_ShouldSetValueAndType()
        {
            var setting = new ApplicationSetting();
            var decimalValue = 123.45m;
            // Używamy InvariantCulture dla spójności z GetDecimalValue, jeśli tam też jest
            setting.SetValue(decimalValue);
            setting.Value.Should().Be(decimalValue.ToString(System.Globalization.CultureInfo.InvariantCulture));
            setting.Type.Should().Be(SettingType.Decimal);
        }

        [Fact]
        public void SetValue_Json_ShouldSerializeAndSetType()
        {
            var setting = new ApplicationSetting();
            var data = new JsonTestData { PropA = "JSON Test", PropB = 789 };
            var expectedJsonString = JsonSerializer.Serialize(data); // Serializujemy bez opcji dla spójności z metodą

            setting.SetValue(data);
            setting.Value.Should().Be(expectedJsonString);
            setting.Type.Should().Be(SettingType.Json);
        }

        // ===== ROZBUDOWANE TESTY DLA ISVALID =====
        [Fact]
        public void IsValid_ShouldWorkCorrectly_ForRequiredAndPattern()
        {
            // Wymagane i puste - niepoprawne
            var setting1 = new ApplicationSetting { IsRequired = true, Value = "" };
            setting1.IsValid().Should().BeFalse();

            // Wymagane i z białymi znakami - niepoprawne
            var setting1b = new ApplicationSetting { IsRequired = true, Value = "   " };
            setting1b.IsValid().Should().BeFalse();

            // Wymagane i niepuste - poprawne (jeśli brak innych walidacji)
            var setting2 = new ApplicationSetting { IsRequired = true, Value = "abc" };
            setting2.IsValid().Should().BeTrue();

            // Wzorzec Regex (np. tylko cyfry)
            var setting3 = new ApplicationSetting { Value = "12345", ValidationPattern = @"^\d+$" };
            setting3.IsValid().Should().BeTrue();

            var setting4 = new ApplicationSetting { Value = "abc12", ValidationPattern = @"^\d+$" };
            setting4.IsValid().Should().BeFalse();

            // Wzorzec i wymagane, ale puste - niepoprawne
            var setting5 = new ApplicationSetting { IsRequired = true, Value = "", ValidationPattern = @"^\d+$" };
            setting5.IsValid().Should().BeFalse();

            // Wzorzec i wymagane, ale nie pasuje - niepoprawne
            var setting6 = new ApplicationSetting { IsRequired = true, Value = "abc", ValidationPattern = @"^\d+$" };
            setting6.IsValid().Should().BeFalse();

            // Wzorzec i wymagane, pasuje - poprawne
            var setting7 = new ApplicationSetting { IsRequired = true, Value = "777", ValidationPattern = @"^\d+$" };
            setting7.IsValid().Should().BeTrue();
        }

        [Theory]
        [InlineData(SettingType.Integer, "123", true)]
        [InlineData(SettingType.Integer, "abc", false)]
        [InlineData(SettingType.Integer, "", false)] // Pusty string nie jest poprawnym intem
        [InlineData(SettingType.Boolean, "true", true)]
        [InlineData(SettingType.Boolean, "yes", false)] // TryParse dla bool jest dość restrykcyjny
        [InlineData(SettingType.Boolean, "", false)] // Pusty string nie jest poprawnym bool
        [InlineData(SettingType.DateTime, "2023-10-26 10:00:00", true)]
        [InlineData(SettingType.DateTime, "not-a-date", false)]
        [InlineData(SettingType.DateTime, "", false)] // Pusty string nie jest poprawną datą
        [InlineData(SettingType.Decimal, "123.45", false)] // Zakładając InvariantCulture w GetDecimalValue
        [InlineData(SettingType.Decimal, "123,45", true)]// Jeśli GetDecimalValue używa InvariantCulture
        [InlineData(SettingType.Decimal, "xyz", false)]
        [InlineData(SettingType.Decimal, "", false)] // Pusty string nie jest poprawnym decimalem
        [InlineData(SettingType.Json, "{\"a\":1}", true)]
        [InlineData(SettingType.Json, "{a:1}", false)] // Niepoprawny JSON
        [InlineData(SettingType.Json, "", true)] // Pusty string może być traktowany jako "poprawny" nie-JSON lub poprawny (null) JSON
        
        public void IsValid_ShouldValidateBasedOnType_WhenNotRequiredAndNoPattern(SettingType type, string value, bool expectedIsValid)
        {
            var setting = new ApplicationSetting { Type = type, Value = value, IsRequired = false, ValidationPattern = null };
            setting.IsValid().Should().Be(expectedIsValid);
        }

        [Fact]
        public void IsValid_ForJson_ShouldHandleNullOrEmptyCorrectly()
        {
            var settingJsonNull = new ApplicationSetting { Type = SettingType.Json, Value = null! }; // Użycie null! aby zasymulować null
            settingJsonNull.IsValid().Should().BeTrue(); // NullOrWhiteSpace jest true dla null

            var settingJsonEmpty = new ApplicationSetting { Type = SettingType.Json, Value = "" };
            settingJsonEmpty.IsValid().Should().BeTrue(); // NullOrWhiteSpace jest true dla ""

            var settingJsonWhitespace = new ApplicationSetting { Type = SettingType.Json, Value = "   " };
            settingJsonWhitespace.IsValid().Should().BeTrue(); // NullOrWhiteSpace jest true dla "   "
                                                               // Metoda IsValidJson zwróci true dla białych znaków, ale jeśli IsRequired=true, to główna metoda IsValid zwróci false.

            var settingJsonRequiredEmpty = new ApplicationSetting { Type = SettingType.Json, Value = "", IsRequired = true };
            settingJsonRequiredEmpty.IsValid().Should().BeFalse();
        }
    }
}