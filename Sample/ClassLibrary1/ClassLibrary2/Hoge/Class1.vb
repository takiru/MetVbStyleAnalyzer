Imports System.IO

Namespace Hoge
    ''' <summary>
    ''' aaa
    ''' </summary>
    Public Class Class1
        ''' <summary>
        ''' aaa
        ''' </summary>
        Public Sub Hoge()
            Dim value = Me.Fuga()
            Dim v = Fuga()
            Dim f As Func(Of String) = AddressOf Fuga
        End Sub

        ''' <summary>
        ''' aaa
        ''' </summary>
        ''' <returns>aaa</returns>
        Public Function Fuga() As String
            Return "Fuga"
        End Function

        ''' <summary>
        ''' aa
        ''' </summary>
        ''' <param name="sender"></param>
        ''' <param name="e"></param>
        Public Sub Test(sender As Object, e As EventArgs)

        End Sub

        ''' <summary>
        ''' aaa
        ''' </summary>
        ''' <param name="hoge">aaa</param>
        ''' <returns>aaa</returns>
        Public Function Piyo(hoge As String) As String
            If 1 = 1 Then
                If 2 = 2 Then
                    If 3 = 3 Then
                        'If 4 = 4 Then
                        Return ""
                        'End If
                    End If
                End If
            End If

            'Select Case 1
            '    Case 1
            '        If 1 = 1 Then
            '            If 2 = 2 Then
            '                If 3 = 3 Then

            '                End If
            '            End If
            '        End If
            'End Select

            Return ""
        End Function
    End Class
End Namespace