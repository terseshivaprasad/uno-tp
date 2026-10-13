using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UnoTP.Models;
using UnoTP.Services.Idfy;

namespace UnoTP.Tests;

/// <summary>The group id every IDfy request made for one application carries (Services/Idfy/IdfyGroup.cs).</summary>
public class IdfyGroupTests
{
    private static readonly UploadFile Copy = new("pan.jpg", "image/jpeg", [1, 2, 3]);

    private const string Completed = """{"status":"completed","task_id":"t","result":{"detected_doc_type":"ind_pan","is_readable":true}}""";

    // An IDfy client answering a request whose address names an application; null for one that names none.
    private static IdfyClient ClientFor(StubNetwork network, string? appNo)
    {
        var context = new DefaultHttpContext();
        if (appNo is not null) context.Request.RouteValues["appNo"] = appNo;
        var group = new IdfyGroup(new HttpContextAccessor { HttpContext = context });
        return new IdfyClient(network.Client(), Options.Create(new IdfyOptions()), NullLogger<IdfyClient>.Instance, group);
    }

    [Fact]
    public void An_application_always_has_the_same_group_id()
    {
        Assert.Equal(IdfyGroup.Of("DEVBB26F10469"), IdfyGroup.Of("DEVBB26F10469"));
        Assert.Equal(IdfyGroup.Of("DEVBB26F10469"), IdfyGroup.Of(" devbb26f10469 "));
    }

    [Fact]
    public void Two_applications_have_different_group_ids()
    {
        Assert.NotEqual(IdfyGroup.Of("DEVBB26F10469"), IdfyGroup.Of("DEVBB26F10478"));
    }

    [Fact]
    public async Task Every_request_for_an_application_carries_its_group_id()
    {
        var expected = IdfyGroup.Of("DEVBB26F10469").ToString();

        var identify = new StubNetwork { Answer = Completed };
        await ClientFor(identify, "DEVBB26F10469").ValidateAsync(Copy, docType: null);

        var read = new StubNetwork { Answer = """{"status":"completed","task_id":"t","result":{"extraction_output":{"id_number":"ABCPE1005E","name_on_card":"A HOLDER"}}}""" };
        await ClientFor(read, "DEVBB26F10469").ExtractPanAsync(Copy);

        var verify = new StubNetwork { Answer = """{"status":"completed","task_id":"t","result":{"source_output":{"status":"id_found"}}}""" };
        await ClientFor(verify, "DEVBB26F10469").VerifyVoterIdAsync("BR0120200012345");

        Assert.Equal(expected, identify.Sent.GetProperty("groupId").GetString());
        Assert.Equal(expected, read.Sent.GetProperty("groupId").GetString());
        Assert.Equal(expected, verify.Sent.GetProperty("groupId").GetString());
    }

    [Fact]
    public async Task What_was_read_and_what_was_identified_carry_the_task_id_of_their_request()
    {
        var read = new StubNetwork { Answer = """{"status":"completed","task_id":"task-read-1","result":{"extraction_output":{"id_number":"ABCPE1005E","name_on_card":"A HOLDER"}}}""" };
        var reading = await new IdfyOcr(ClientFor(read, "DEVBB26F10469")).ReadAsync(DocumentKind.PanCard, "", Copy, new OcrSubject("", "", ""), consent: false);

        var identify = new StubNetwork { Answer = """{"status":"completed","task_id":"task-identify-1","result":{"detected_doc_type":"ind_pan","is_readable":true}}""" };
        var identified = await new IdfyDocumentIdentifier(ClientFor(identify, "DEVBB26F10469")).IdentifyAsync(DocumentKind.PanCard, "", Copy);

        Assert.Equal("task-read-1", reading.TaskId);
        Assert.Equal("task-identify-1", identified.TaskId);
        Assert.True(identified.Matches);
    }

    [Fact]
    public void A_reading_with_only_a_task_id_read_nothing()
    {
        Assert.False(new OcrReading { TaskId = "task-1" }.ReadAnything);
        Assert.True(new OcrReading(Name: "A HOLDER") { TaskId = "task-1" }.ReadAnything);
    }

    [Fact]
    public async Task Every_request_is_sent_with_a_task_id_of_its_own()
    {
        var first = new StubNetwork { Answer = Completed };
        var second = new StubNetwork { Answer = Completed };

        await ClientFor(first, "DEVBB26F10469").ValidateAsync(Copy, docType: null);
        await ClientFor(second, "DEVBB26F10469").ValidateAsync(Copy, docType: null);

        var firstId = first.Sent.GetProperty("taskId").GetString();
        var secondId = second.Sent.GetProperty("taskId").GetString();
        Assert.True(Guid.TryParse(firstId, out _));
        Assert.True(Guid.TryParse(secondId, out _));
        Assert.NotEqual(firstId, secondId);
    }

    [Fact]
    public async Task A_request_about_no_application_sends_no_group_id()
    {
        var network = new StubNetwork { Answer = Completed };

        await ClientFor(network, appNo: null).ValidateAsync(Copy, docType: null);

        Assert.False(network.Sent.TryGetProperty("groupId", out _));
    }
}
