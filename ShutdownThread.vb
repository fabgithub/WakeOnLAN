'    WakeOnLAN - Wake On LAN
'    Copyright (C) 2004-2014 Aquila Technology, LLC. <webmaster@aquilatech.com>
'
'    This file is part of WakeOnLAN.
'
'    WakeOnLAN is free software: you can redistribute it and/or modify
'    it under the terms of the GNU General Public License as published by
'    the Free Software Foundation, either version 3 of the License, or
'    (at your option) any later version.
'
'    WakeOnLAN is distributed in the hope that it will be useful,
'    but WITHOUT ANY WARRANTY; without even the implied warranty of
'    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
'    GNU General Public License for more details.
'
'    You should have received a copy of the GNU General Public License
'    along with WakeOnLAN.  If not, see <http://www.gnu.org/licenses/>.

Imports System.Threading
Imports System.ComponentModel
Imports System.Management

Public Class ShutdownThread
    Public Enum Action
        None
        Abort
        Shutdown
        Sleep
        Hibernate
        User
    End Enum

    Private WithEvents BackgroundWorker1 As New BackgroundWorker
    Private _item As ListViewItem
    Private _progressbar As ProgressBar
    Private _action As Action
    Private _Message As String
    Private _delay As Integer
    Private _force As Boolean
    Private _reboot As Boolean
    Private errMessage As String

    Public Sub New(ByVal item As ListViewItem, ByVal progressbar As ProgressBar, ByVal action As Action, ByVal Message As String, ByVal Delay As Integer, ByVal Force As Boolean, ByVal Reboot As Boolean)
        _item = item
        _progressbar = progressbar
        _action = action
        _Message = Message
        _delay = Delay
        _force = Force
        _reboot = Reboot
        errMessage = ""
        BackgroundWorker1.RunWorkerAsync()
    End Sub

    Private Sub DoWork(ByVal sender As Object, ByVal e As DoWorkEventArgs) Handles BackgroundWorker1.DoWork
        Dim dwResult As Integer
        Dim dwReason As Long
        Dim sMachine As String
        Dim sAlertMessage As String
        Dim dwDelay As Long
        Dim dwForce As Long
        Dim dwReboot As Long
        Dim m As Machine

        dwReason = 0L
        dwResult = 0
        sAlertMessage = _Message & vbNullChar
        dwDelay = _delay
        dwForce = CLng(_force)
        dwReboot = CLng(_reboot)

        m = Machines(_item.Text)
        sMachine = "\\" & m.Netbios

        _item.SubItems(1).ForeColor = Color.FromKnownColor(KnownColor.WindowText)

        If (_action <> Action.Abort And m.ShutdownCommand.Length > 0) Then _action = Action.User

        Try
            Select Case _action
                Case Action.Abort
                    dwResult = AbortSystemShutdown(sMachine)

                Case Action.Shutdown
                    dwResult = InitiateSystemShutdown(sMachine, sAlertMessage, dwDelay, dwForce, dwReboot)

                Case Action.User
                    Shell(m.ShutdownCommand, AppWinStyle.Hide, False)

                Case Action.Sleep, Action.Hibernate
                    dwResult = WMIpower(sMachine)

            End Select

        Catch ex As Exception
            errMessage = ex.Message
            e.Result = 0
            Return

        End Try

        If dwResult = 0 Then
            errMessage = FormatMessage(Err.LastDllError)
        End If
        e.Result = dwResult

    End Sub

    Private Function WMIpower(sMachine As String) As Integer
        Dim process As ManagementClass
        Dim path As ManagementPath
        Dim options As ConnectionOptions = New ConnectionOptions()
        Dim inparams, outparams As ManagementBaseObject
        Dim ProcID, retval As String

        process = New ManagementClass("Win32_Process")
        path = New ManagementPath(String.Format("{0}\root\cimv2", sMachine))

#If False Then
        options.Username = ""
        options.Password = ""
        process.Scope = New ManagementScope(path, options)
#Else
        process.Scope = New ManagementScope(path)
#End If
        process.Scope.Connect()

        inparams = process.GetMethodParameters("Create")
        Select Case _action
            Case Action.Sleep
                inparams("CommandLine") = "rundll32.exe powrprof.dll,SetSuspendState Standby"

            Case Action.Hibernate
                inparams("CommandLine") = "rundll32.exe powrprof.dll,SetSuspendState Hibernate"

        End Select

        outparams = process.InvokeMethod("Create", inparams, Nothing)
        ProcID = outparams("ProcessID").ToString()
        retval = outparams("ReturnValue").ToString()

        Return IIf(retval, 0, 1)
    End Function

    Private Sub backgroundWorker1_RunWorkerCompleted(ByVal sender As Object, ByVal e As RunWorkerCompletedEventArgs) Handles BackgroundWorker1.RunWorkerCompleted

        With _item.SubItems(1)
            If e.Result = 0 Then
                .ForeColor = Color.Red
                .Text = String.Format(My.Resources.Strings.ErrorMsg, errMessage)
                .Tag = .Text ' error
            Else
                .ForeColor = Color.Green
                .Text = My.Resources.Strings.Successful
                .Tag = "" ' success
            End If
        End With

        With _progressbar
            .Increment(1)
            If (ShutdownMode = True) And (.Value = .Maximum) Then
                Shutdown.Complete()
            End If
        End With

    End Sub

End Class
