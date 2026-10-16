using GroupFinance.Core.Constants;

namespace GroupFinance.UnitTests;

public sealed class ApplicationConstantsTests
{
    [Fact]
    public void Application_Uses_Npr_Currency()
    {
        Assert.Equal("NPR", ApplicationConstants.Currency);
    }

    [Fact]
    public void Application_Uses_DdMmYyyy_Date_Format()
    {
        Assert.Equal("dd/MM/yyyy", ApplicationConstants.DefaultDateFormat);
    }
}
