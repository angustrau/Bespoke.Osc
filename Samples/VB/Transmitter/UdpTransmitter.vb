﻿Public Class UdpTransmitter
    Implements ITransmitter

    Public Sub Start(packet As OscPacket) Implements ITransmitter.Start
        Assert.ParamIsNotNull(packet)

        mPacket = packet
        OscPacket.UdpClient = New UdpClient(SourcePort)
        Thread.VolatileWrite(mSendMessages, True)

        mTransmitterThread = New Thread(AddressOf RunWorker)
        mTransmitterThread.Start()
    End Sub

    Public Sub [Stop]() Implements ITransmitter.Stop
        Thread.VolatileWrite(mSendMessages, False)
        mTransmitterThread.Join()
    End Sub

    Private Sub RunWorker()
        Try
            While Thread.VolatileRead(mSendMessages)
                mPacket.Send(Destination)

                mTransmissionCount += 1
                Console.Clear()
                Console.WriteLine("Osc Transmitter: Udp")
                Console.WriteLine("Transmission Count: {0}{1}", mTransmissionCount, Environment.NewLine)
                Console.WriteLine("Press any key to exit.")

                Thread.Sleep(1000)
            End While
        Catch ex As Exception
            Console.WriteLine(ex.Message)
        End Try
    End Sub

    Private ReadOnly Destination As IPEndPoint = New IPEndPoint(IPAddress.Loopback, Program.Port)
    Private ReadOnly SourcePort As Integer = 10024

    Private mSendMessages As Boolean
    Private mTransmitterThread As Thread
    Private mPacket As OscPacket
    Private mTransmissionCount As Integer
End Class
