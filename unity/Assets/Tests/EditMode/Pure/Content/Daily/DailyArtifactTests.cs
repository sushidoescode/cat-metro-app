using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using CatMetro.Content;
using CatMetro.Content.Daily;

namespace CatMetro.Tests.Daily
{
    // CM-C6 criteria 6 + 7 (library half): the artifact is serialised in-memory with one record
    // per date {dateKey, k, seed, verdict, stageVerdicts, solverCompletionTicks}, the seed lines
    // are single-sourced and anchored, and two identical runs render byte-identical text. The
    // wrapper (tests/daily/daily-pipeline.test.sh) proves the file-level half through the host.
    public sealed class DailyArtifactTests
    {
        private static DailyRunReport ThreeDateReport()
        {
            var dates = DateKeys.Enumerate("2026-08-24", 3);
            return DFixtures.Run(DFixtures.Request(
                dates, new DFixtures.FixedFactory(DFixtures.L001Dto())));
        }

        // Criterion 6a: the JSON record shape, for every date.
        [Test]
        public void ToJson_CarriesTheRecordShapePerDate()
        {
            var root = JObject.Parse(ThreeDateReport().ToJson());
            var dates = (JArray)root["dates"];
            Assert.That(dates, Is.Not.Null);
            Assert.That(dates.Count, Is.EqualTo(3));
            foreach (var t in dates)
            {
                var o = (JObject)t;
                foreach (var key in new[]
                    { "dateKey", "k", "seed", "verdict", "stageVerdicts", "solverCompletionTicks" })
                    Assert.That(o[key], Is.Not.Null, $"record key '{key}' missing");
                var stages = (JArray)o["stageVerdicts"];
                Assert.That(stages.Count, Is.GreaterThanOrEqualTo(11));
                foreach (var s in stages)
                    foreach (var key in new[] { "stage", "code", "detail", "value", "blocks" })
                        Assert.That(((JObject)s)[key], Is.Not.Null, $"stage key '{key}' missing");
            }
        }

        // Criterion 7 (library half): identical inputs render byte-identical artifacts.
        [Test]
        public void ToJson_MatchesHistoricalArtifactBytes()
        {
            // Complete pre-change historical artifact bytes, hand-checked from the default
            // CM-DAILY-1 run. Keep this independent of the production serializer.
            var expected = System.Text.Encoding.UTF8.GetString(System.Convert.FromBase64String(
                "ewogICJnZW5lcmF0b3IiOiAiQ00tREFJTFktMSIsCiAgImJvYXJkUHJvdmVuYW5jZSI6ICJ0ZXN0LXN0dWIiLAogICJkYXRlcyI6IFsKICAgIHsKICAgICAg" +
                "ImRhdGVLZXkiOiAiMjAyNi0wOC0yNCIsCiAgICAgICJrIjogMCwKICAgICAgInNlZWQiOiA4NTU3NzAyNzIsCiAgICAgICJ2ZXJkaWN0IjogIlBhc3MiLAog" +
                "ICAgICAiZGV0YWlsIjogIiIsCiAgICAgICJzdGFnZVZlcmRpY3RzIjogWwogICAgICAgIHsKICAgICAgICAgICJzdGFnZSI6ICJTY2hlbWEiLAogICAgICAg" +
                "ICAgImNvZGUiOiAiUGFzcyIsCiAgICAgICAgICAiZGV0YWlsIjogIiIsCiAgICAgICAgICAidmFsdWUiOiAic2NoZW1hIHYyIE9LIiwKICAgICAgICAgICJi" +
                "bG9ja3MiOiBmYWxzZQogICAgICAgIH0sCiAgICAgICAgewogICAgICAgICAgInN0YWdlIjogIlN0YXRpY0FuYWx5c2lzIiwKICAgICAgICAgICJjb2RlIjog" +
                "Ildhcm4iLAogICAgICAgICAgImRldGFpbCI6ICJzdGF0aW9uIEJMVSBpcyBhIGRlY295OiBubyBzb3VyY2UgZW1pdHMgYW55IGNvbG91ciBpdCBhY2NlcHRz" +
                "IiwKICAgICAgICAgICJ2YWx1ZSI6ICIiLAogICAgICAgICAgImJsb2NrcyI6IGZhbHNlCiAgICAgICAgfSwKICAgICAgICB7CiAgICAgICAgICAic3RhZ2Ui" +
                "OiAiTG93ZXJCb3VuZEZlYXNpYmlsaXR5IiwKICAgICAgICAgICJjb2RlIjogIlVuY29uZmlndXJlZCIsCiAgICAgICAgICAiZGV0YWlsIjogIlVOQ09ORklH" +
                "VVJFRChsb3dlckJvdW5kU2xhY2spIiwKICAgICAgICAgICJ2YWx1ZSI6ICJsb3dlckJvdW5kPTQ0IChtaW5UcmF2ZWxUaWNrcz0yMiB4IGRlbGl2ZXJpZXM9" +
                "MikiLAogICAgICAgICAgImJsb2NrcyI6IGZhbHNlCiAgICAgICAgfSwKICAgICAgICB7CiAgICAgICAgICAic3RhZ2UiOiAiU29sdmVyIiwKICAgICAgICAg" +
                "ICJjb2RlIjogIlBhc3MiLAogICAgICAgICAgImRldGFpbCI6ICIiLAogICAgICAgICAgInZhbHVlIjogInRpY2tzPTUwIHN3aXRjaGVzPTEgd2lkdGg9MCBu" +
                "b2Rlcz0xOTQwIHBpbm5lZD0xNTIiLAogICAgICAgICAgImJsb2NrcyI6IGZhbHNlCiAgICAgICAgfSwKICAgICAgICB7CiAgICAgICAgICAic3RhZ2UiOiAi" +
                "VHJpdmlhbGl0eVJlamVjdCIsCiAgICAgICAgICAiY29kZSI6ICJQYXNzIiwKICAgICAgICAgICJkZXRhaWwiOiAiIiwKICAgICAgICAgICJ2YWx1ZSI6ICJ6" +
                "ZXJvLWlucHV0IHJ1biBwaW5uZWQgKHBpbm5lZCBORVctUTQ6IGEgbm9uLW1hdGNoaW5nIGNhdCBhcnJpdmVkIGF0IGEgc3RhdGlvbiDigJQgcmVqZWN0aW9u" +
                "L3JldmVyc2UgdHJhdmVyc2FsIGlzIG91dCBvZiBDTS1DMSBzY29wZSAoc3RhdGUvYmFja2xvZy5tZCBRLUIsIGNyaXRlcmlvbiAxNCkpIOKAlCBub3QgYSB3" +
                "aW4iLAogICAgICAgICAgImJsb2NrcyI6IGZhbHNlCiAgICAgICAgfSwKICAgICAgICB7CiAgICAgICAgICAic3RhZ2UiOiAiQnJpdHRsZW5lc3NBY2Nlc3Np" +
                "YmlsaXR5IiwKICAgICAgICAgICJjb2RlIjogIlBhc3MiLAogICAgICAgICAgImRldGFpbCI6ICIiLAogICAgICAgICAgInZhbHVlIjogInJldGVudGlvbj0x" +
                "MDAlICh3aW5zPTIwIGxvc3Nlcz0wIHBpbm5lZD0wKSB3aW5kb3dzPVsxOF0iLAogICAgICAgICAgImJsb2NrcyI6IGZhbHNlCiAgICAgICAgfSwKICAgICAg" +
                "ICB7CiAgICAgICAgICAic3RhZ2UiOiAiU3RhckNoZWNrIiwKICAgICAgICAgICJjb2RlIjogIlVuY29uZmlndXJlZCIsCiAgICAgICAgICAiZGV0YWlsIjog" +
                "IlVOQ09ORklHVVJFRChzdGFyQmFuZFNsYWNrKSIsCiAgICAgICAgICAidmFsdWUiOiAidHdvPTIwMCB0aHJlZT0zMDAiLAogICAgICAgICAgImJsb2NrcyI6" +
                "IGZhbHNlCiAgICAgICAgfSwKICAgICAgICB7CiAgICAgICAgICAic3RhZ2UiOiAiRGlmZmljdWx0eUNoZWNrIiwKICAgICAgICAgICJjb2RlIjogIlVuY29u" +
                "ZmlndXJlZCIsCiAgICAgICAgICAiZGV0YWlsIjogIlVOQ09ORklHVVJFRChheGlzQkJhbmRDYXBzKSIsCiAgICAgICAgICAidmFsdWUiOiAiQj04IHBlYWs4" +
                "MD0yIGVudHJvcHk9MCBDPTEgVD0wLjMxMjUgSD04IFBBUlRJQUwoUS1KKSBSPTEvMiIsCiAgICAgICAgICAiYmxvY2tzIjogZmFsc2UKICAgICAgICB9LAog" +
                "ICAgICAgIHsKICAgICAgICAgICJzdGFnZSI6ICJOb3ZlbHR5Q2hlY2siLAogICAgICAgICAgImNvZGUiOiAiU2tpcHBlZCIsCiAgICAgICAgICAiZGV0YWls" +
                "IjogIlNLSVBQRUQobm9uLWNhbXBhaWduKSIsCiAgICAgICAgICAidmFsdWUiOiAiIiwKICAgICAgICAgICJibG9ja3MiOiBmYWxzZQogICAgICAgIH0sCiAg" +
                "ICAgICAgewogICAgICAgICAgInN0YWdlIjogIlN0YWxlbmVzcyIsCiAgICAgICAgICAiY29kZSI6ICJTdGFsZSIsCiAgICAgICAgICAiZGV0YWlsIjogInJl" +
                "ZmVyZW5jZSB0aW1lc3RhbXAgdW5hdmFpbGFibGUg4oCUIHRyZWF0ZWQgYXMgc3RhbGUg4oCUIGNvbXB1dGVkLCBwcmludGVkLCBub24tYmxvY2tpbmcgd2hp" +
                "bGUgUS1PIGlzIG9wZW4iLAogICAgICAgICAgInZhbHVlIjogIiIsCiAgICAgICAgICAiYmxvY2tzIjogZmFsc2UKICAgICAgICB9LAogICAgICAgIHsKICAg" +
                "ICAgICAgICJzdGFnZSI6ICJIdW1hblBsYXl0ZXN0IiwKICAgICAgICAgICJjb2RlIjogIlBlbmRpbmciLAogICAgICAgICAgImRldGFpbCI6ICJIVU1BTi1W" +
                "RVJJRklFRCAocGVuZGluZykg4oCUIENJIGNhbm5vdCBydW4gdGhpcyBzdGFnZSAoQURSLTAwMDk6MzUpOyBkZXBlbmRzIG9uIEQtNiIsCiAgICAgICAgICAi" +
                "dmFsdWUiOiAidGVzdGVycz0xIiwKICAgICAgICAgICJibG9ja3MiOiBmYWxzZQogICAgICAgIH0sCiAgICAgICAgewogICAgICAgICAgInN0YWdlIjogIldl" +
                "ZWtkYXlSYW1wIiwKICAgICAgICAgICJjb2RlIjogIlVuY29uZmlndXJlZCIsCiAgICAgICAgICAiZGV0YWlsIjogIlVOQ09ORklHVVJFRChORVctUTIxKSIs" +
                "CiAgICAgICAgICAidmFsdWUiOiAiZHQ9MC4wOCIsCiAgICAgICAgICAiYmxvY2tzIjogZmFsc2UKICAgICAgICB9CiAgICAgIF0sCiAgICAgICJzb2x2ZXJD" +
                "b21wbGV0aW9uVGlja3MiOiA1MAogICAgfSwKICAgIHsKICAgICAgImRhdGVLZXkiOiAiMjAyNi0wOC0yNSIsCiAgICAgICJrIjogMCwKICAgICAgInNlZWQi" +
                "OiAyMDM5MDE0NTM5LAogICAgICAidmVyZGljdCI6ICJQYXNzIiwKICAgICAgImRldGFpbCI6ICIiLAogICAgICAic3RhZ2VWZXJkaWN0cyI6IFsKICAgICAg" +
                "ICB7CiAgICAgICAgICAic3RhZ2UiOiAiU2NoZW1hIiwKICAgICAgICAgICJjb2RlIjogIlBhc3MiLAogICAgICAgICAgImRldGFpbCI6ICIiLAogICAgICAg" +
                "ICAgInZhbHVlIjogInNjaGVtYSB2MiBPSyIsCiAgICAgICAgICAiYmxvY2tzIjogZmFsc2UKICAgICAgICB9LAogICAgICAgIHsKICAgICAgICAgICJzdGFn" +
                "ZSI6ICJTdGF0aWNBbmFseXNpcyIsCiAgICAgICAgICAiY29kZSI6ICJXYXJuIiwKICAgICAgICAgICJkZXRhaWwiOiAic3RhdGlvbiBCTFUgaXMgYSBkZWNv" +
                "eTogbm8gc291cmNlIGVtaXRzIGFueSBjb2xvdXIgaXQgYWNjZXB0cyIsCiAgICAgICAgICAidmFsdWUiOiAiIiwKICAgICAgICAgICJibG9ja3MiOiBmYWxz" +
                "ZQogICAgICAgIH0sCiAgICAgICAgewogICAgICAgICAgInN0YWdlIjogIkxvd2VyQm91bmRGZWFzaWJpbGl0eSIsCiAgICAgICAgICAiY29kZSI6ICJVbmNv" +
                "bmZpZ3VyZWQiLAogICAgICAgICAgImRldGFpbCI6ICJVTkNPTkZJR1VSRUQobG93ZXJCb3VuZFNsYWNrKSIsCiAgICAgICAgICAidmFsdWUiOiAibG93ZXJC" +
                "b3VuZD00NCAobWluVHJhdmVsVGlja3M9MjIgeCBkZWxpdmVyaWVzPTIpIiwKICAgICAgICAgICJibG9ja3MiOiBmYWxzZQogICAgICAgIH0sCiAgICAgICAg" +
                "ewogICAgICAgICAgInN0YWdlIjogIlNvbHZlciIsCiAgICAgICAgICAiY29kZSI6ICJQYXNzIiwKICAgICAgICAgICJkZXRhaWwiOiAiIiwKICAgICAgICAg" +
                "ICJ2YWx1ZSI6ICJ0aWNrcz01MCBzd2l0Y2hlcz0xIHdpZHRoPTAgbm9kZXM9MTk0MCBwaW5uZWQ9MTUyIiwKICAgICAgICAgICJibG9ja3MiOiBmYWxzZQog" +
                "ICAgICAgIH0sCiAgICAgICAgewogICAgICAgICAgInN0YWdlIjogIlRyaXZpYWxpdHlSZWplY3QiLAogICAgICAgICAgImNvZGUiOiAiUGFzcyIsCiAgICAg" +
                "ICAgICAiZGV0YWlsIjogIiIsCiAgICAgICAgICAidmFsdWUiOiAiemVyby1pbnB1dCBydW4gcGlubmVkIChwaW5uZWQgTkVXLVE0OiBhIG5vbi1tYXRjaGlu" +
                "ZyBjYXQgYXJyaXZlZCBhdCBhIHN0YXRpb24g4oCUIHJlamVjdGlvbi9yZXZlcnNlIHRyYXZlcnNhbCBpcyBvdXQgb2YgQ00tQzEgc2NvcGUgKHN0YXRlL2Jh" +
                "Y2tsb2cubWQgUS1CLCBjcml0ZXJpb24gMTQpKSDigJQgbm90IGEgd2luIiwKICAgICAgICAgICJibG9ja3MiOiBmYWxzZQogICAgICAgIH0sCiAgICAgICAg" +
                "ewogICAgICAgICAgInN0YWdlIjogIkJyaXR0bGVuZXNzQWNjZXNzaWJpbGl0eSIsCiAgICAgICAgICAiY29kZSI6ICJQYXNzIiwKICAgICAgICAgICJkZXRh" +
                "aWwiOiAiIiwKICAgICAgICAgICJ2YWx1ZSI6ICJyZXRlbnRpb249MTAwJSAod2lucz0yMCBsb3NzZXM9MCBwaW5uZWQ9MCkgd2luZG93cz1bMThdIiwKICAg" +
                "ICAgICAgICJibG9ja3MiOiBmYWxzZQogICAgICAgIH0sCiAgICAgICAgewogICAgICAgICAgInN0YWdlIjogIlN0YXJDaGVjayIsCiAgICAgICAgICAiY29k" +
                "ZSI6ICJVbmNvbmZpZ3VyZWQiLAogICAgICAgICAgImRldGFpbCI6ICJVTkNPTkZJR1VSRUQoc3RhckJhbmRTbGFjaykiLAogICAgICAgICAgInZhbHVlIjog" +
                "InR3bz0yMDAgdGhyZWU9MzAwIiwKICAgICAgICAgICJibG9ja3MiOiBmYWxzZQogICAgICAgIH0sCiAgICAgICAgewogICAgICAgICAgInN0YWdlIjogIkRp" +
                "ZmZpY3VsdHlDaGVjayIsCiAgICAgICAgICAiY29kZSI6ICJVbmNvbmZpZ3VyZWQiLAogICAgICAgICAgImRldGFpbCI6ICJVTkNPTkZJR1VSRUQoYXhpc0JC" +
                "YW5kQ2FwcykiLAogICAgICAgICAgInZhbHVlIjogIkI9OCBwZWFrODA9MiBlbnRyb3B5PTAgQz0xIFQ9MC4zMTI1IEg9OCBQQVJUSUFMKFEtSikgUj0xLzIi" +
                "LAogICAgICAgICAgImJsb2NrcyI6IGZhbHNlCiAgICAgICAgfSwKICAgICAgICB7CiAgICAgICAgICAic3RhZ2UiOiAiTm92ZWx0eUNoZWNrIiwKICAgICAg" +
                "ICAgICJjb2RlIjogIlNraXBwZWQiLAogICAgICAgICAgImRldGFpbCI6ICJTS0lQUEVEKG5vbi1jYW1wYWlnbikiLAogICAgICAgICAgInZhbHVlIjogIiIs" +
                "CiAgICAgICAgICAiYmxvY2tzIjogZmFsc2UKICAgICAgICB9LAogICAgICAgIHsKICAgICAgICAgICJzdGFnZSI6ICJTdGFsZW5lc3MiLAogICAgICAgICAg" +
                "ImNvZGUiOiAiU3RhbGUiLAogICAgICAgICAgImRldGFpbCI6ICJyZWZlcmVuY2UgdGltZXN0YW1wIHVuYXZhaWxhYmxlIOKAlCB0cmVhdGVkIGFzIHN0YWxl" +
                "IOKAlCBjb21wdXRlZCwgcHJpbnRlZCwgbm9uLWJsb2NraW5nIHdoaWxlIFEtTyBpcyBvcGVuIiwKICAgICAgICAgICJ2YWx1ZSI6ICIiLAogICAgICAgICAg" +
                "ImJsb2NrcyI6IGZhbHNlCiAgICAgICAgfSwKICAgICAgICB7CiAgICAgICAgICAic3RhZ2UiOiAiSHVtYW5QbGF5dGVzdCIsCiAgICAgICAgICAiY29kZSI6" +
                "ICJQZW5kaW5nIiwKICAgICAgICAgICJkZXRhaWwiOiAiSFVNQU4tVkVSSUZJRUQgKHBlbmRpbmcpIOKAlCBDSSBjYW5ub3QgcnVuIHRoaXMgc3RhZ2UgKEFE" +
                "Ui0wMDA5OjM1KTsgZGVwZW5kcyBvbiBELTYiLAogICAgICAgICAgInZhbHVlIjogInRlc3RlcnM9MSIsCiAgICAgICAgICAiYmxvY2tzIjogZmFsc2UKICAg" +
                "ICAgICB9LAogICAgICAgIHsKICAgICAgICAgICJzdGFnZSI6ICJXZWVrZGF5UmFtcCIsCiAgICAgICAgICAiY29kZSI6ICJVbmNvbmZpZ3VyZWQiLAogICAg" +
                "ICAgICAgImRldGFpbCI6ICJVTkNPTkZJR1VSRUQoTkVXLVEyMSkiLAogICAgICAgICAgInZhbHVlIjogImR0PTAuMDgiLAogICAgICAgICAgImJsb2NrcyI6" +
                "IGZhbHNlCiAgICAgICAgfQogICAgICBdLAogICAgICAic29sdmVyQ29tcGxldGlvblRpY2tzIjogNTAKICAgIH0sCiAgICB7CiAgICAgICJkYXRlS2V5Ijog" +
                "IjIwMjYtMDgtMjYiLAogICAgICAiayI6IDAsCiAgICAgICJzZWVkIjogMjEwOTYxOTM4NiwKICAgICAgInZlcmRpY3QiOiAiUGFzcyIsCiAgICAgICJkZXRh" +
                "aWwiOiAiIiwKICAgICAgInN0YWdlVmVyZGljdHMiOiBbCiAgICAgICAgewogICAgICAgICAgInN0YWdlIjogIlNjaGVtYSIsCiAgICAgICAgICAiY29kZSI6" +
                "ICJQYXNzIiwKICAgICAgICAgICJkZXRhaWwiOiAiIiwKICAgICAgICAgICJ2YWx1ZSI6ICJzY2hlbWEgdjIgT0siLAogICAgICAgICAgImJsb2NrcyI6IGZh" +
                "bHNlCiAgICAgICAgfSwKICAgICAgICB7CiAgICAgICAgICAic3RhZ2UiOiAiU3RhdGljQW5hbHlzaXMiLAogICAgICAgICAgImNvZGUiOiAiV2FybiIsCiAg" +
                "ICAgICAgICAiZGV0YWlsIjogInN0YXRpb24gQkxVIGlzIGEgZGVjb3k6IG5vIHNvdXJjZSBlbWl0cyBhbnkgY29sb3VyIGl0IGFjY2VwdHMiLAogICAgICAg" +
                "ICAgInZhbHVlIjogIiIsCiAgICAgICAgICAiYmxvY2tzIjogZmFsc2UKICAgICAgICB9LAogICAgICAgIHsKICAgICAgICAgICJzdGFnZSI6ICJMb3dlckJv" +
                "dW5kRmVhc2liaWxpdHkiLAogICAgICAgICAgImNvZGUiOiAiVW5jb25maWd1cmVkIiwKICAgICAgICAgICJkZXRhaWwiOiAiVU5DT05GSUdVUkVEKGxvd2Vy" +
                "Qm91bmRTbGFjaykiLAogICAgICAgICAgInZhbHVlIjogImxvd2VyQm91bmQ9NDQgKG1pblRyYXZlbFRpY2tzPTIyIHggZGVsaXZlcmllcz0yKSIsCiAgICAg" +
                "ICAgICAiYmxvY2tzIjogZmFsc2UKICAgICAgICB9LAogICAgICAgIHsKICAgICAgICAgICJzdGFnZSI6ICJTb2x2ZXIiLAogICAgICAgICAgImNvZGUiOiAi" +
                "UGFzcyIsCiAgICAgICAgICAiZGV0YWlsIjogIiIsCiAgICAgICAgICAidmFsdWUiOiAidGlja3M9NTAgc3dpdGNoZXM9MSB3aWR0aD0wIG5vZGVzPTE5NDAg" +
                "cGlubmVkPTE1MiIsCiAgICAgICAgICAiYmxvY2tzIjogZmFsc2UKICAgICAgICB9LAogICAgICAgIHsKICAgICAgICAgICJzdGFnZSI6ICJUcml2aWFsaXR5" +
                "UmVqZWN0IiwKICAgICAgICAgICJjb2RlIjogIlBhc3MiLAogICAgICAgICAgImRldGFpbCI6ICIiLAogICAgICAgICAgInZhbHVlIjogInplcm8taW5wdXQg" +
                "cnVuIHBpbm5lZCAocGlubmVkIE5FVy1RNDogYSBub24tbWF0Y2hpbmcgY2F0IGFycml2ZWQgYXQgYSBzdGF0aW9uIOKAlCByZWplY3Rpb24vcmV2ZXJzZSB0" +
                "cmF2ZXJzYWwgaXMgb3V0IG9mIENNLUMxIHNjb3BlIChzdGF0ZS9iYWNrbG9nLm1kIFEtQiwgY3JpdGVyaW9uIDE0KSkg4oCUIG5vdCBhIHdpbiIsCiAgICAg" +
                "ICAgICAiYmxvY2tzIjogZmFsc2UKICAgICAgICB9LAogICAgICAgIHsKICAgICAgICAgICJzdGFnZSI6ICJCcml0dGxlbmVzc0FjY2Vzc2liaWxpdHkiLAog" +
                "ICAgICAgICAgImNvZGUiOiAiUGFzcyIsCiAgICAgICAgICAiZGV0YWlsIjogIiIsCiAgICAgICAgICAidmFsdWUiOiAicmV0ZW50aW9uPTEwMCUgKHdpbnM9" +
                "MjAgbG9zc2VzPTAgcGlubmVkPTApIHdpbmRvd3M9WzE4XSIsCiAgICAgICAgICAiYmxvY2tzIjogZmFsc2UKICAgICAgICB9LAogICAgICAgIHsKICAgICAg" +
                "ICAgICJzdGFnZSI6ICJTdGFyQ2hlY2siLAogICAgICAgICAgImNvZGUiOiAiVW5jb25maWd1cmVkIiwKICAgICAgICAgICJkZXRhaWwiOiAiVU5DT05GSUdV" +
                "UkVEKHN0YXJCYW5kU2xhY2spIiwKICAgICAgICAgICJ2YWx1ZSI6ICJ0d289MjAwIHRocmVlPTMwMCIsCiAgICAgICAgICAiYmxvY2tzIjogZmFsc2UKICAg" +
                "ICAgICB9LAogICAgICAgIHsKICAgICAgICAgICJzdGFnZSI6ICJEaWZmaWN1bHR5Q2hlY2siLAogICAgICAgICAgImNvZGUiOiAiVW5jb25maWd1cmVkIiwK" +
                "ICAgICAgICAgICJkZXRhaWwiOiAiVU5DT05GSUdVUkVEKGF4aXNCQmFuZENhcHMpIiwKICAgICAgICAgICJ2YWx1ZSI6ICJCPTggcGVhazgwPTIgZW50cm9w" +
                "eT0wIEM9MSBUPTAuMzEyNSBIPTggUEFSVElBTChRLUopIFI9MS8yIiwKICAgICAgICAgICJibG9ja3MiOiBmYWxzZQogICAgICAgIH0sCiAgICAgICAgewog" +
                "ICAgICAgICAgInN0YWdlIjogIk5vdmVsdHlDaGVjayIsCiAgICAgICAgICAiY29kZSI6ICJTa2lwcGVkIiwKICAgICAgICAgICJkZXRhaWwiOiAiU0tJUFBF" +
                "RChub24tY2FtcGFpZ24pIiwKICAgICAgICAgICJ2YWx1ZSI6ICIiLAogICAgICAgICAgImJsb2NrcyI6IGZhbHNlCiAgICAgICAgfSwKICAgICAgICB7CiAg" +
                "ICAgICAgICAic3RhZ2UiOiAiU3RhbGVuZXNzIiwKICAgICAgICAgICJjb2RlIjogIlN0YWxlIiwKICAgICAgICAgICJkZXRhaWwiOiAicmVmZXJlbmNlIHRp" +
                "bWVzdGFtcCB1bmF2YWlsYWJsZSDigJQgdHJlYXRlZCBhcyBzdGFsZSDigJQgY29tcHV0ZWQsIHByaW50ZWQsIG5vbi1ibG9ja2luZyB3aGlsZSBRLU8gaXMg" +
                "b3BlbiIsCiAgICAgICAgICAidmFsdWUiOiAiIiwKICAgICAgICAgICJibG9ja3MiOiBmYWxzZQogICAgICAgIH0sCiAgICAgICAgewogICAgICAgICAgInN0" +
                "YWdlIjogIkh1bWFuUGxheXRlc3QiLAogICAgICAgICAgImNvZGUiOiAiUGVuZGluZyIsCiAgICAgICAgICAiZGV0YWlsIjogIkhVTUFOLVZFUklGSUVEIChw" +
                "ZW5kaW5nKSDigJQgQ0kgY2Fubm90IHJ1biB0aGlzIHN0YWdlIChBRFItMDAwOTozNSk7IGRlcGVuZHMgb24gRC02IiwKICAgICAgICAgICJ2YWx1ZSI6ICJ0" +
                "ZXN0ZXJzPTEiLAogICAgICAgICAgImJsb2NrcyI6IGZhbHNlCiAgICAgICAgfSwKICAgICAgICB7CiAgICAgICAgICAic3RhZ2UiOiAiV2Vla2RheVJhbXAi" +
                "LAogICAgICAgICAgImNvZGUiOiAiVW5jb25maWd1cmVkIiwKICAgICAgICAgICJkZXRhaWwiOiAiVU5DT05GSUdVUkVEKE5FVy1RMjEpIiwKICAgICAgICAg" +
                "ICJ2YWx1ZSI6ICJkdD0wLjA4IiwKICAgICAgICAgICJibG9ja3MiOiBmYWxzZQogICAgICAgIH0KICAgICAgXSwKICAgICAgInNvbHZlckNvbXBsZXRpb25U" +
                "aWNrcyI6IDUwCiAgICB9CiAgXSwKICAiZXhpdEZhaWx1cmUiOiBmYWxzZQp9"
            ));
            Assert.That(ThreeDateReport().ToJson(), Is.EqualTo(expected));
            Assert.That(ThreeDateReport().ToJson(), Is.EqualTo(expected));
        }

        [Test]
        public void ToJson_RuntimeSchemeStartsWithItsGeneratorLabel()
        {
            var historical = DFixtures.Request(new[] { "2026-08-24" },
                new DFixtures.FixedFactory(DFixtures.L001Dto()));
            var runtime = new DailyRunRequest(
                historical.SchemaBytes,
                historical.ValidatorConfig,
                historical.PipelineConfig,
                historical.WeekdayCurveBytes,
                historical.DateKeys,
                historical.Factory,
                historical.ReferenceTimestamp,
                historical.BoardProvenance,
                DailyLineSeedScheme.Instance,
                historical.MaxNodesExpanded);

            Assert.That(DFixtures.Run(runtime).ToJson(),
                Does.StartWith("{\n  \"generator\": \"CM-DAILY-\""));
            Assert.That(ThreeDateReport().ToJson(),
                Does.StartWith("{\n  \"generator\": \"CM-DAILY-1\""),
                "the historical artifact prefix must remain byte-identical");
        }

        // Criterion 6b (format half): one anchored line per date, nothing else DAILY_SEED-shaped.
        [Test]
        public void SeedLines_OnePerDate_AnchoredFormat()
        {
            var report = ThreeDateReport();
            var lines = report.SeedLines();
            Assert.That(lines.Count, Is.EqualTo(3));
            var rx = new Regex(@"^DAILY_SEED \d{4}-\d{2}-\d{2} \d+ \d+$");
            foreach (var line in lines)
                Assert.That(rx.IsMatch(line), Is.True, $"malformed seed line: {line}");
            Assert.That(lines.Distinct().Count(), Is.EqualTo(3));
            for (int i = 0; i < 3; i++)
            {
                var rec = report.Records[i];
                Assert.That(lines[i],
                    Is.EqualTo($"DAILY_SEED {rec.DateKey} {rec.K} {rec.Seed}"));
            }
        }

        // The serialiser bridge (criterion 5's mechanism): a corpus DTO round-trips through
        // DailyBoardJson -> LevelImporter to the identical content.
        [Test]
        public void RoundTrip_L001_ReimportsToTheSameContent()
        {
            var dto = DFixtures.L001Dto();
            var text = DailyBoardJson.Serialize(dto);
            var back = LevelImporter.Import(System.Text.Encoding.UTF8.GetBytes(text));
            Assert.That(back.Ok, Is.True, $"round-trip must import: {back.Error}");
            var b = back.Value.Dto;

            Assert.That(b.Id, Is.EqualTo(dto.Id));
            Assert.That(b.Seed, Is.EqualTo(dto.Seed));
            Assert.That(b.SchemaVersion, Is.EqualTo(dto.SchemaVersion));
            Assert.That(b.Meta.Band, Is.EqualTo(dto.Meta.Band));
            Assert.That(b.Meta.NewMechanic, Is.EqualTo(dto.Meta.NewMechanic));
            Assert.That(b.Meta.MinActionWindowTicks, Is.EqualTo(dto.Meta.MinActionWindowTicks));
            Assert.That(b.Meta.HasValidatedAt, Is.EqualTo(dto.Meta.HasValidatedAt));
            Assert.That(b.Nodes.Length, Is.EqualTo(dto.Nodes.Length));
            Assert.That(b.Edges.Length, Is.EqualTo(dto.Edges.Length));
            Assert.That(b.Waves.Length, Is.EqualTo(dto.Waves.Length));
            Assert.That(b.Win.TimeLimitTicks, Is.EqualTo(dto.Win.TimeLimitTicks));
            Assert.That(b.Win.Deliveries, Is.EqualTo(dto.Win.Deliveries));
            Assert.That(b.Economy.BaseTickets, Is.EqualTo(dto.Economy.BaseTickets));
        }

        // Absent-key conventions survive: L001 authors no queueCapacity and no validatedAt, so
        // the serialised text must not invent either key (AMD-09: tooling deletes the key rather
        // than write null).
        [Test]
        public void RoundTrip_PreservesAbsentKeys()
        {
            var text = DailyBoardJson.Serialize(DFixtures.L001Dto());
            Assert.That(text, Does.Not.Contain("queueCapacity"));
            Assert.That(text, Does.Not.Contain("validatedAt"));
        }

        // solverCompletionTicks is the CM-C5 stage-4 figure (CompletionTicks — the sim-tick the
        // final delivery lands on, RunToEnd().Tick - 1 by the CM-C1 convention).
        [Test]
        public void SolverCompletionTicks_PositiveForASolvedBoard()
        {
            var rec = ThreeDateReport().Records[0];
            Assert.That(rec.SolverCompletionTicks, Is.GreaterThan(0));
        }
    }
}
