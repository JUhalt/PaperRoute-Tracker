Namespace Models

    ' What kind of work a manuscript is (#64). Unspecified until the user or
    ' an import says; nothing is inferred.
    Public Enum WorkType
        Unspecified
        JournalArticle
        Preprint
        ConferencePaper
        ConferenceAbstract
        Poster
        BookChapter
        Thesis
        Other
    End Enum

End Namespace
