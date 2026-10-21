using RollRadar.Api.Common;

namespace RollRadar.Api.Tests;

public class CsvParserTests
{
    [Fact]
    public void Reads_quoted_fields_with_commas_quotes_and_line_breaks()
    {
        const string csv = "﻿ingles,portugues,categoria,tambem_conhecido_como\r\n" +
                           "Side Control,Cem quilos,Posições,\"Controle lateral, 100 kg\"\r\n" +
                           "\"Say \"\"hi\"\"\",Oi,Fundamentos,\"line one\nline two\"\n";

        var records = CsvParser.Parse(csv);

        Assert.Equal(3, records.Count);
        Assert.Equal("ingles", records[0][0]);
        Assert.Equal(["Side Control", "Cem quilos", "Posições", "Controle lateral, 100 kg"], records[1]);
        Assert.Equal("Say \"hi\"", records[2][0]);
        Assert.Equal("line one\nline two", records[2][3]);
    }

    [Fact]
    public void RequireColumns_rejects_files_missing_a_column()
    {
        var header = CsvParser.Parse("name,age\nana,30\n")[0];

        Assert.Throws<FormatException>(() => CsvParser.RequireColumns(header, ["ingles", "portugues"], "techniques catalog"));
    }
}
