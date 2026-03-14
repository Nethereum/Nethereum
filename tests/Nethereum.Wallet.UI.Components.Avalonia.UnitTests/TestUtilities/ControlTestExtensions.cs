using Avalonia.Controls;
using Avalonia.Threading;
using FluentAssertions.Execution;
using System;
using System.Threading.Tasks;

namespace Nethereum.Wallet.UI.Components.Avalonia.UnitTests.TestUtilities;

/// <summary>
/// Extension methods for testing Avalonia controls with improved assertions
/// </summary>
public static class ControlTestExtensions
{
    /// <summary>
    /// Asserts that a control has the specified value with proper UI thread execution
    /// </summary>
    public static T ShouldHaveValue<T>(this Control control, string propertyName, T expectedValue) where T : class
    {
        var actualValue = Dispatcher.UIThread.Invoke(() =>
        {
            var property = control.GetType().GetProperty(propertyName);
            return property?.GetValue(control) as T;
        });

        using (new AssertionScope())
        {
            actualValue.Should().Be(expectedValue, $"because {propertyName} should equal {expectedValue}");
        }

        return actualValue!;
    }

    /// <summary>
    /// Asserts that a control property equals the expected value
    /// </summary>
    public static void ShouldHaveProperty<T>(this Control control, string propertyName, T expectedValue)
    {
        var actualValue = Dispatcher.UIThread.Invoke(() =>
        {
            var property = control.GetType().GetProperty(propertyName);
            return property?.GetValue(control);
        });

        using (new AssertionScope())
        {
            actualValue.Should().Be(expectedValue, $"because {propertyName} should equal {expectedValue}");
        }
    }

    /// <summary>
    /// Asserts that a control is in an error state
    /// </summary>
    public static void ShouldHaveError(this Control control, string? expectedErrorText = null)
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            var errorProperty = control.GetType().GetProperty("Error");
            var errorTextProperty = control.GetType().GetProperty("ErrorText");

            var hasError = errorProperty?.GetValue(control) as bool?;
            var errorText = errorTextProperty?.GetValue(control) as string;

            using (new AssertionScope())
            {
                hasError.Should().BeTrue("because the control should be in an error state");

                if (expectedErrorText != null)
                {
                    errorText.Should().Be(expectedErrorText, "because the error text should match");
                }
            }
        });
    }

    /// <summary>
    /// Asserts that a control is not in an error state
    /// </summary>
    public static void ShouldNotHaveError(this Control control)
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            var errorProperty = control.GetType().GetProperty("Error");
            var errorTextProperty = control.GetType().GetProperty("ErrorText");

            var hasError = errorProperty?.GetValue(control) as bool?;
            var errorText = errorTextProperty?.GetValue(control) as string;

            using (new AssertionScope())
            {
                hasError.Should().BeFalse("because the control should not be in an error state");
                errorText.Should().BeNullOrEmpty("because there should be no error text");
            }
        });
    }

    /// <summary>
    /// Simulates setting a value on a control and waiting for the change to propagate
    /// </summary>
    public static async Task<T> SetValueAsync<T>(this T control, string propertyName, object value) where T : Control
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var property = control.GetType().GetProperty(propertyName);
            property?.SetValue(control, value);
        });

        // Small delay to allow for property change propagation
        await Task.Delay(10);
        return control;
    }

    /// <summary>
    /// Simulates a user click on a control
    /// </summary>
    public static async Task<T> ClickAsync<T>(this T control) where T : Control
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            // Simulate a click by invoking any click-related commands or methods
            var clickMethod = control.GetType().GetMethod("OnClick",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (clickMethod != null)
            {
                try
                {
                    clickMethod.Invoke(control, null);
                }
                catch
                {
                    // Ignore if method doesn't exist or fails
                }
            }
        });

        await Task.Delay(10); // Allow for event propagation
        return control;
    }

    /// <summary>
    /// Gets a property value from a control safely on the UI thread
    /// </summary>
    public static T? GetPropertyValue<T>(this Control control, string propertyName)
    {
        return Dispatcher.UIThread.Invoke(() =>
        {
            var property = control.GetType().GetProperty(propertyName);
            var value = property?.GetValue(control);
            return value is T typedValue ? typedValue : default;
        });
    }

    /// <summary>
    /// Waits for a condition to be true with a timeout
    /// </summary>
    public static async Task<bool> WaitForConditionAsync(this Control control, Func<bool> condition, TimeSpan? timeout = null)
    {
        var actualTimeout = timeout ?? TimeSpan.FromSeconds(5);
        var start = DateTime.UtcNow;

        while (DateTime.UtcNow - start < actualTimeout)
        {
            var result = await Dispatcher.UIThread.InvokeAsync(condition);
            if (result)
                return true;

            await Task.Delay(50);
        }

        return false;
    }
}