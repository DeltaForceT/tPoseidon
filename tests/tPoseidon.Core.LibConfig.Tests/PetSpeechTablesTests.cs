namespace tPoseidon.Core.LibConfig.Tests;

public class PetSpeechTablesTests
{
    private const string SyntheticXml = """
        <?xml version="1.0" encoding="utf-8"?>
        <INI TableCount="3" TableInfoCount="2">
        <TABLE name="PetParam" RowCount="3" TableInfoID="1548" FieldCnt="4">
            <ROW><ID>4001</ID><Speech><![CDATA[PetSpeech_Fox]]></Speech><Voice>0</Voice><SpeechColor>1</SpeechColor></ROW>
            <ROW><ID>4002</ID><Speech><![CDATA[]]></Speech><Voice>1</Voice><SpeechColor>0</SpeechColor></ROW>
            <ROW><ID>500000</ID><Speech><![CDATA[PetSpeech_Fox]]></Speech><Voice>0</Voice><SpeechColor>0</SpeechColor></ROW>
        </TABLE>
        <TABLE name="PetSpeech_Fox" RowCount="2" TableInfoID="1547" FieldCnt="10">
            <ROW>
                <Type>9</Type>
                <Speech0><![CDATA[Sit well]]></Speech0><Voice0><![CDATA[ ]]></Voice0><Ratio0>0.300000</Ratio0>
                <Speech1><![CDATA[Rest up]]></Speech1><Voice1><![CDATA[v1]]></Voice1><Ratio1>0.100000</Ratio1>
                <Speech2><![CDATA[ ]]></Speech2><Voice2><![CDATA[ ]]></Voice2><Ratio2>0.500000</Ratio2>
            </ROW>
            <ROW>
                <Type>10</Type>
                <Speech0><![CDATA[Nap time]]></Speech0><Voice0><![CDATA[ ]]></Voice0><Ratio0>0.000000</Ratio0>
                <Speech1><![CDATA[ ]]></Speech1><Voice1><![CDATA[ ]]></Voice1><Ratio1>0.000000</Ratio1>
                <Speech2><![CDATA[ ]]></Speech2><Voice2><![CDATA[ ]]></Voice2><Ratio2>0.000000</Ratio2>
            </ROW>
        </TABLE>
        <TABLE name="Unrelated" RowCount="1" TableInfoID="9" FieldCnt="1">
            <ROW><X>1</X></ROW>
        </TABLE>
        </INI>
        """;

    private static PetSpeechTables Load()
    {
        var path = Path.Combine(Path.GetTempPath(), $"petspeech-{Guid.NewGuid():N}.xml");
        try
        {
            File.WriteAllText(path, SyntheticXml);
            return new LibConfigXmlReader(path).LoadPetSpeechTables();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void PetParamKeepsOnlyChrTypesInsideTheValidRange()
    {
        var tables = Load();

        Assert.Equal([4001, 4002], tables.Params.Keys.Order());
        Assert.Equal(new PetSpeechParam("PetSpeech_Fox", false, 1), tables.Params[4001]);
        Assert.Equal(new PetSpeechParam(string.Empty, true, 0), tables.Params[4002]);
    }

    [Fact]
    public void LinesCarryCumulativeThousandthThresholdsAndStopAtTheFirstBlankSpeech()
    {
        var lines = Load().Tables["PetSpeech_Fox"][9];

        Assert.Equal(
            [new PetSpeechLine("Sit well", string.Empty, 300), new PetSpeechLine("Rest up", "v1", 400)],
            lines);
    }

    [Fact]
    public void ASingleLineRowKeepsItsLineWithAZeroThreshold()
    {
        Assert.Equal([new PetSpeechLine("Nap time", string.Empty, 0)], Load().Tables["PetSpeech_Fox"][10]);
    }

    [Fact]
    public void OnlyTablesWithThePetSpeechPrefixAreLoaded()
    {
        Assert.Equal(["PetSpeech_Fox"], Load().Tables.Keys);
    }
}
