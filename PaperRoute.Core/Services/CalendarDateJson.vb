Imports System
Imports System.IO
Imports System.Text
Imports System.Text.RegularExpressions

Namespace Services

    ' Calendar dates in the library file (#118): a decision dated 9 October
    ' is 9 October on every computer. System.Text.Json writes a local-kind
    ' DateTime with the machine's offset and reads any offset-bearing value
    ' back in the current zone, so a date recorded just after midnight read
    ' as the day before after a flight west, and the load that cuts reminder
    ' and follow-up dates to the day made the shift permanent. A converter
    ' would be the usual answer, but its Read takes a ref struct, which
    ' Visual Basic cannot declare, so the file's text is adjusted at the
    ' boundary instead: a date-and-time value with an offset, under any
    ' property but a ...Utc stamp, loses the offset on the way in and on the
    ' way out, and is then read as written and written without one. A value
    ' ending in Z is an instant and is left alone. v0.9 reads both forms, so
    ' there is no schema step.
    Public NotInheritable Class CalendarDateJson

        ' "SubmittedDate": "2026-10-09T00:00:00+14:00" and the like: the
        ' property name, the date and time, then the offset to drop.
        Private Shared ReadOnly OffsetDate As New Regex(
            "(?<date>""[^""\\]*(?<!Utc)""\s*:\s*""\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?)[+-]\d{2}:\d{2}""",
            RegexOptions.Compiled Or RegexOptions.CultureInvariant
        )


        Private Sub New()
        End Sub


        Public Shared Function WithoutOffsets(
            json As String
        ) As String

            If String.IsNullOrEmpty(json) Then
                Return json
            End If

            Return OffsetDate.Replace(
                json,
                "${date}"""
            )

        End Function


        ' The library file's text, read as written.
        Public Shared Function Read(
            stream As Stream
        ) As String

            Using reader As New StreamReader(
                stream,
                New UTF8Encoding(False),
                detectEncodingFromByteOrderMarks:=True,
                bufferSize:=65536,
                leaveOpen:=True
            )

                Return WithoutOffsets(
                    reader.ReadToEnd()
                )

            End Using

        End Function


        ' Serialized text written without offsets, as the serializer writes a
        ' stream: UTF-8 with no byte order mark.
        Public Shared Sub Write(
            stream As Stream,
            json As String
        )

            Dim bytes As Byte() =
                New UTF8Encoding(False).GetBytes(
                    WithoutOffsets(json)
                )

            stream.Write(
                bytes,
                0,
                bytes.Length
            )

        End Sub

    End Class

End Namespace
