using System.Xml.Serialization;

namespace MURO.Infrastructure.Services;

// ─── Ortak Taban Sınıfı ──────────────────────────────────────────────────

[XmlRoot("response")]
public class BbbBaseResponse
{
    [XmlElement("returncode")]
    public string ReturnCode { get; set; } = string.Empty;

    [XmlElement("message")]
    public string? Message { get; set; }
}

// ─── Özel Yanıtlar ───────────────────────────────────────────────────────

[XmlRoot("response")]
public class BbbCreateMeetingResponse : BbbBaseResponse
{
    [XmlElement("meetingID")]
    public string? MeetingId { get; set; }
}

[XmlRoot("response")]
public class BbbIsMeetingRunningResponse : BbbBaseResponse
{
    [XmlElement("running")]
    public string Running { get; set; } = "false";
}

[XmlRoot("response")]
public class BbbGetRecordingsResponse : BbbBaseResponse
{
    [XmlArray("recordings")]
    [XmlArrayItem("recording")]
    public List<BbbXmlRecording> Recordings { get; set; } = new();
}

[XmlRoot("response")]
public class BbbGetMeetingsResponse : BbbBaseResponse
{
    [XmlArray("meetings")]
    [XmlArrayItem("meeting")]
    public List<BbbXmlMeetingInfo> Meetings { get; set; } = new();
}

// GetMeetingInfo direkt olarak <response> tagı altında <meetingID> vb. özellikleri de barındırıyor
[XmlRoot("response")]
public class BbbGetMeetingInfoResponse : BbbXmlMeetingInfo
{
    [XmlElement("returncode")]
    public string ReturnCode { get; set; } = string.Empty;

    [XmlElement("message")]
    public string? Message { get; set; }
}


// ─── Alt Bileşenler (Alt Sınıflar) ────────────────────────────────────────

public class BbbXmlMeetingInfo
{
    [XmlElement("meetingID")]
    public string? MeetingId { get; set; }

    [XmlElement("meetingName")]
    public string? MeetingName { get; set; }

    [XmlElement("running")]
    public string Running { get; set; } = "false";

    [XmlElement("participantCount")]
    public int ParticipantCount { get; set; }

    [XmlElement("moderatorCount")]
    public int ModeratorCount { get; set; }

    [XmlElement("listenerCount")]
    public int ListenerCount { get; set; }

    [XmlElement("videoCount")]
    public int VideoCount { get; set; }

    [XmlElement("voiceParticipantCount")]
    public int VoiceParticipantCount { get; set; }

    [XmlElement("recording")]
    public string IsRecording { get; set; } = "false";

    [XmlElement("isBreakout")]
    public string IsBreakout { get; set; } = "false";

    [XmlElement("startTime")]
    public string? StartTime { get; set; }

    [XmlElement("duration")]
    public string? Duration { get; set; }

    [XmlElement("maxUsers")]
    public int MaxUsers { get; set; }

    [XmlArray("attendees")]
    [XmlArrayItem("attendee")]
    public List<BbbXmlAttendee> Attendees { get; set; } = new();

    [XmlArray("breakoutRooms")]
    [XmlArrayItem("breakout")]
    public List<BbbXmlBreakout> BreakoutRooms { get; set; } = new();
}

public class BbbXmlAttendee
{
    [XmlElement("fullName")]
    public string? FullName { get; set; }

    [XmlElement("role")]
    public string? Role { get; set; }

    [XmlElement("isPresenter")]
    public string IsPresenter { get; set; } = "false";

    [XmlElement("hasJoinedVoice")]
    public string HasJoinedVoice { get; set; } = "false";

    [XmlElement("hasVideo")]
    public string HasVideo { get; set; } = "false";

    [XmlElement("clientType")]
    public string? ClientType { get; set; }
}

public class BbbXmlBreakout
{
    [XmlElement("meetingID")]
    public string? MeetingId { get; set; }

    [XmlElement("name")]
    public string? Name { get; set; }

    [XmlElement("participantCount")]
    public int ParticipantCount { get; set; }
}

public class BbbXmlRecording
{
    [XmlElement("recordID")]
    public string? RecordId { get; set; }

    [XmlElement("meetingID")]
    public string? MeetingId { get; set; }

    [XmlElement("state")]
    public string? State { get; set; }

    [XmlElement("startTime")]
    public string? StartTime { get; set; }

    [XmlElement("duration")]
    public string? Duration { get; set; }

    [XmlArray("playback")]
    [XmlArrayItem("format")]
    public List<BbbXmlPlaybackFormat> PlaybackFormats { get; set; } = new();
}

public class BbbXmlPlaybackFormat
{
    [XmlElement("type")]
    public string? Type { get; set; }

    [XmlElement("url")]
    public string? Url { get; set; }
}
