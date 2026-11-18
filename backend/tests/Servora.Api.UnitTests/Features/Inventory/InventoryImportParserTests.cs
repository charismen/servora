using System.Text;
using Servora.Api.Features.Inventory;

namespace Servora.Api.UnitTests.Features.Inventory;

public class InventoryImportParserTests
{
    [Fact]
    public void Parse_ReturnsValidRowsForCsv()
    {
        using var stream = Csv("sku,name,category,purchase price,starting stock\nRAM-8GB,DDR4 8GB,Memory,150000,3");

        var rows = InventoryImportParser.Parse(stream, "items.csv", ["Memory"], new HashSet<string>());

        Assert.Single(rows);
        Assert.True(rows[0].IsValid);
        Assert.Equal("Create", rows[0].Action);
        Assert.Equal(3, rows[0].Item!.StartingQuantity);
    }

    [Fact]
    public void Parse_SupportsIndonesianHeaders()
    {
        using var stream = Csv("SKU,Nama Barang,Kategori,Harga Beli,Stok Awal\nRAM-16GB,DDR4 16GB,Memory,Rp250000,2");

        var rows = InventoryImportParser.Parse(stream, "items.csv", ["Memory"], new HashSet<string>());

        Assert.Single(rows);
        Assert.True(rows[0].IsValid);
        Assert.Equal(250000, rows[0].Item!.PurchasePrice);
        Assert.Equal(2, rows[0].Item!.StartingQuantity);
    }

    [Fact]
    public void Parse_AcceptsMarginPercentWithOrWithoutPercentSymbol()
    {
        using var stream = Csv("sku,name,margin\nRAM-8GB,DDR4 8GB,15\nRAM-16GB,DDR4 16GB,20%");

        var rows = InventoryImportParser.Parse(stream, "items.csv", [], new HashSet<string>());

        Assert.All(rows, row => Assert.True(row.IsValid));
        Assert.Equal(15, rows[0].Item!.MarginPercent);
        Assert.Equal(20, rows[1].Item!.MarginPercent);
    }

    [Fact]
    public void TemplateFiles_ParseAsValidImportRows()
    {
        using var csv = Csv(InventoryImportParser.BuildCsvTemplate());
        using var xlsx = new MemoryStream(InventoryImportParser.BuildXlsxTemplate());

        var csvRows = InventoryImportParser.Parse(csv, "template.csv", ["Spare Part"], new HashSet<string>());
        var xlsxRows = InventoryImportParser.Parse(xlsx, "template.xlsx", ["Spare Part"], new HashSet<string>());

        Assert.True(csvRows[0].IsValid, csvRows[0].ErrorMessage);
        Assert.True(xlsxRows[0].IsValid, xlsxRows[0].ErrorMessage);
        Assert.Equal("RAM-8GB-DDR4", xlsxRows[0].Item!.Sku);
    }

    [Fact]
    public void Parse_FlagsDuplicateSkuInFile()
    {
        using var stream = Csv("sku,name\nSSD-1TB,SSD\nSSD-1TB,SSD Duplicate");

        var rows = InventoryImportParser.Parse(stream, "items.csv", [], new HashSet<string>());

        Assert.False(rows[1].IsValid);
        Assert.Contains("Duplicate SKU", rows[1].ErrorMessage);
    }

    [Fact]
    public void Parse_FlagsInvalidNegativePrice()
    {
        using var stream = Csv("sku,name,purchase price\nBAT-1,Battery,-1");

        var rows = InventoryImportParser.Parse(stream, "items.csv", [], new HashSet<string>());

        Assert.False(rows[0].IsValid);
        Assert.Contains("Purchase price cannot be negative", rows[0].ErrorMessage);
    }

    [Fact]
    public void Parse_FlagsUnknownCategory()
    {
        using var stream = Csv("sku,name,category\nKB-1,Keyboard,Unknown");

        var rows = InventoryImportParser.Parse(stream, "items.csv", ["Keyboard"], new HashSet<string>());

        Assert.False(rows[0].IsValid);
        Assert.Contains("Category was not found", rows[0].ErrorMessage);
    }

    private static MemoryStream Csv(string text) => new(Encoding.UTF8.GetBytes(text));
}
