Namespace Hoge
    Public Class Class1
        Public Sub Hoge()
            Dim value = Me.Fuga()
            Dim v = Fuga()
            Dim f As Func(Of String) = AddressOf Fuga
        End Sub

        Public Function Fuga() As String
            Return "Fuga"
        End Function

        ''' <summary>
        ''' 
        ''' </summary>
        ''' <param name="hoge"></param>
        ''' <returns></returns>
        Public Function Piyo(hoge As String) As String
            Return ""
        End Function
    End Class
End Namespace