// BEGIN-FEAT::BE-662::2026-07-01::AHL::Generación de CUFE (dId) para PA con algoritmo real portado del monitoreo viejo (GENERATE_CUFE.cs)
using System.Xml.Linq;
using Monitoreo.Worker.Models;

namespace Monitoreo.Worker.Services.Certification;

public class CufeGenerationService : ICufeGenerationService
{
    private readonly ILogger<CufeGenerationService> _logger;
    private static readonly XNamespace Fe = "http://dgi-fep.mef.gob.pa";

    public CufeGenerationService(
        IHttpClientFactory httpClientFactory,
        ILogger<CufeGenerationService> logger)
    {
        _logger = logger;
    }

    public Task<CufeResult> GenerateCufeAsync(string xmlContent, CountryConfig config, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var doc = XDocument.Parse(xmlContent);
        var gDGen = doc.Root?.Element(Fe + "gDGen")
            ?? throw new InvalidOperationException("gDGen no encontrado en rFE de PA");

        var gRucEmi = gDGen.Element(Fe + "gEmis")?.Element(Fe + "gRucEmi")
            ?? throw new InvalidOperationException("gEmis/gRucEmi no encontrado en rFE de PA");

        var tipoDoc = gDGen.Element(Fe + "iDoc")?.Value ?? "01";
        var tipoContribuyente = gRucEmi.Element(Fe + "dTipoRuc")?.Value ?? "2";
        var ruc = gRucEmi.Element(Fe + "dRuc")?.Value
            ?? throw new InvalidOperationException("dRuc no encontrado en rFE de PA");
        var sucursal = gDGen.Element(Fe + "gEmis")?.Element(Fe + "dSucEm")?.Value ?? "0001";
        var fechaEmision = DateTimeOffset.Parse(gDGen.Element(Fe + "dFechaEm")?.Value
            ?? throw new InvalidOperationException("dFechaEm no encontrado en rFE de PA"));
        var numFactura = gDGen.Element(Fe + "dNroDF")?.Value
            ?? throw new InvalidOperationException("dNroDF no encontrado en rFE de PA");
        var puntoFacturacion = gDGen.Element(Fe + "dPtoFacDF")?.Value ?? "001";
        var tipoEmision = gDGen.Element(Fe + "iTpEmis")?.Value ?? "01";
        var ambiente = gDGen.Element(Fe + "iAmb")?.Value ?? "1";
        var seguridad = gDGen.Element(Fe + "dSeg")?.Value
            ?? throw new InvalidOperationException("dSeg no encontrado en rFE de PA");

        var dvRuc = CalcularDvRuc(ruc);
        if (dvRuc == "-1")
            throw new InvalidOperationException($"No se pudo calcular DV del RUC {ruc}");

        var cuerpo =
            tipoDoc.PadLeft(2, '0') +
            tipoContribuyente +
            ruc.PadLeft(20, '0') +
            "-" + dvRuc.PadLeft(2, '0') +
            int.Parse(sucursal).ToString().PadLeft(4, '0') +
            fechaEmision.ToString("yyyyMMdd") +
            numFactura.PadLeft(10, '0') +
            puntoFacturacion.PadLeft(3, '0') +
            tipoEmision.PadLeft(2, '0') +
            ambiente +
            seguridad.PadLeft(9, '0');

        var dv = GenerarDvCufe(cuerpo);
        if (dv == -1)
            throw new InvalidOperationException("No se pudo generar el dígito verificador del CUFE");

        var cufe = "FE" + cuerpo + dv;

        // Inyectar en dId
        var dId = doc.Root?.Element(Fe + "dId");
        if (dId != null) dId.Value = cufe;
        else doc.Root?.AddFirst(new XElement(Fe + "dId", cufe));

        _logger.LogDebug("CUFE PA generado: {Cufe}", cufe);
        return Task.FromResult(new CufeResult(cufe, string.Empty, doc.ToString(SaveOptions.DisableFormatting)));
    }

    // ===== Algoritmo portado 1:1 de GENERATE_CUFE.cs del monitoreo viejo =====

    private static int SumarDigitos(int num)
    {
        var sum = 0;
        while (num != 0) { sum += num % 10; num /= 10; }
        return sum;
    }

    private static int ObtenerUltimoDigito(char c)
    {
        var s = ((int)c).ToString();
        return int.Parse(s[^1].ToString());
    }

    private static int GenerarDvCufe(string cufe)
    {
        try
        {
            var multiplicador = 2;
            var cambio = -1;
            var sumaDigitos = 0;
            foreach (var num in cufe)
            {
                sumaDigitos += !int.TryParse(num.ToString(), out var numConvert)
                    ? SumarDigitos(ObtenerUltimoDigito(num) * multiplicador)
                    : SumarDigitos(numConvert * multiplicador);
                multiplicador += cambio;
                cambio *= -1;
            }
            var resto = sumaDigitos % 10;
            return resto == 0 ? 0 : 10 - resto;
        }
        catch { return -1; }
    }

    private static string CalcularDvRuc(string ruc)
    {
        try
        {
            var rs = ruc.Split('-');
            if ((rs.Length == 4 && rs[1] != "NT") || rs.Length < 3 || rs.Length > 5) return "-1";
            var sw = false;
            string ructb;

            if (ruc[0] == 'E')
            {
                ructb = new string('0', Max0(4 - rs[1].Length)) + "00000050050" + new string('0', Max0(3 - rs[1].Length)) + rs[1] + new string('0', Max0(5 - rs[2].Length)) + rs[2];
            }
            else if (rs[1] == "NT")
            {
                ructb = new string('0', Max0(4 - rs[1].Length)) + "0000005" + new string('0', Max0(2 - rs[0][..Max0(rs[0].Length - 2)].Length) * 2) +
                        rs[0][..Max0(rs[0].Length - 2)] + "43" + new string('0', Max0(3 - rs[2].Length)) + rs[2] + new string('0', Max0(5 - rs[3].Length)) + rs[3];
            }
            else if (rs[0][..Max0(rs[0].Length - 2)] == "AV")
            {
                ructb = new string('0', Max0(4 - rs[1].Length)) + "0000005" + new string('0', Max0(2 - rs[0][..Max0(rs[0].Length - 2)].Length) * 2) +
                        rs[0][..Max0(rs[0].Length - 2)] + "15" + new string('0', Max0(3 - rs[1].Length)) + rs[1] + new string('0', Max0(5 - rs[2].Length)) + rs[2];
            }
            else if (rs[1] == "PI")
            {
                ructb = new string('0', Max0(4 - rs[1].Length)) + "0000005" + new string('0', Max0(2 - rs[0][..Max0(rs[0].Length - 2)].Length) * 2) +
                        rs[0][..Max0(rs[0].Length - 2)] + "79" + new string('0', Max0(3 - rs[1].Length)) + rs[1] + new string('0', Max0(5 - rs[2].Length)) + rs[2];
            }
            else if (rs[0] == "PE")
            {
                ructb = new string('0', Max0(4 - rs[1].Length)) + "00000050075" + new string('0', Max0(3 - rs[1].Length)) + rs[1] + new string('0', Max0(5 - rs[2].Length)) + rs[2];
            }
            else if (ruc.Length > 1 && ruc[1] == 'N')
            {
                ructb = new string('0', Max0(4 - rs[1].Length)) + "00000050040" + new string('0', Max0(3 - rs[1].Length)) + rs[1] + new string('0', Max0(5 - rs[2].Length)) + rs[2];
            }
            else if (rs[0].Length > 0 && rs[0].Length <= 2)
            {
                ructb = new string('0', Max0(4 - rs[1].Length)) + "0000005" + new string('0', Max0(2 - rs[0].Length)) + rs[0] + "00" + new string('0', Max0(3 - rs[1].Length)) + rs[1] + new string('0', Max0(5 - rs[2].Length)) + rs[2];
            }
            else // RUC juridico
            {
                ructb = new string('0', Max0(10 - rs[0].Length)) + rs[0] + new string('0', Max0(4 - rs[1].Length)) + rs[1] + new string('0', Max0(6 - rs[2].Length)) + rs[2];
                sw = ructb[3] == '0' && ructb[4] == '0' && int.Parse(ructb[5].ToString()) < 5;
            }

            var dv1 = DigitoDv(sw, ructb);
            var dv2 = DigitoDv(sw, ructb + (char)(48 + dv1));
            return dv1.ToString() + dv2.ToString();
        }
        catch { return "-1"; }
    }

    private static int DigitoDv(bool sw, string ructb)
    {
        var j = 2;
        var nsuma = 0;
        for (var c = ructb.Length - 1; c >= 0; c--)
        {
            if (sw && j == 12) { sw = false; j -= 1; }
            nsuma += j * (ructb[c] - '0');
            j++;
        }
        var r = nsuma % 11;
        return r > 1 ? 11 - r : 0;
    }

    private static int Max0(int n) => n > 0 ? n : 0;
}
// END-FEAT::BE-662::2026-07-01::AHL::Generación de CUFE (dId) para PA con algoritmo real portado del monitoreo viejo
