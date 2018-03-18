﻿using Engine.Helpers;
using System;
using System.Net.Sockets;
using System.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading;

namespace Engine.Network
{
  /// <summary>
  /// Server connection to client.
  /// </summary>
  sealed class ServerConnection :
    Connection
  {
    #region consts
    /// <summary>
    /// Время неактивности соединения, после прошествия которого соединение будет закрыто.
    /// </summary>
    public const int SilenceTimeout = 7 * 1000;

    /// <summary>
    /// Время ожидания регистрации. После того как данное время закончится соединение будет закрыто.
    /// </summary>
    public const int UnregisteredTimeout = 60 * 1000;
    #endregion

    #region private field
    [SecurityCritical] private readonly string _serverApiName;
    [SecurityCritical] private readonly DateTime _createTime;
    [SecurityCritical] private readonly Logger _logger;

    [SecurityCritical] private DateTime _lastActivity;

    [SecurityCritical] private EventHandler<PackageReceivedEventArgs> _receivedCallback;
    #endregion

    #region constructors

    /// <summary>
    /// Creates server connection.
    /// </summary>
    /// <param name="handler">Connected to client socket.</param>
    /// <param name="certificate">Server certificate.</param>
    /// <param name="apiName">Current api version.</param>
    /// <param name="logger">Logger</param>
    /// <param name="receivedCallback">Callback for received data.</param>
    [SecurityCritical]
    public ServerConnection(Socket handler, X509Certificate2 certificate, string apiName, Logger logger, EventHandler<PackageReceivedEventArgs> receivedCallback)
      : base(certificate, logger)
    {
      Construct(handler, ConnectionState.HandshakeRequestWait);

      _serverApiName = apiName;
      _createTime = DateTime.UtcNow;
      _lastActivity = DateTime.UtcNow;

      _receivedCallback = receivedCallback ?? throw new ArgumentNullException();

      _logger = logger;
    }
    #endregion

    #region properties
    /// <summary>
    /// Interval of time that connection not send any messages to server.
    /// </summary>
    public int SilenceInterval
    {
      [SecurityCritical]
      get
      {
        ThrowIfDisposed();
        return (int)(DateTime.UtcNow - _lastActivity).TotalMilliseconds;
      }
    }

    /// <summary>
    /// Interval of time that connection not registering on server.
    /// </summary>
    public int UnregisteredInterval
    {
      [SecurityCritical]
      get
      {
        ThrowIfDisposed();
        return (IsRegistered) ? 0 : (int)(DateTime.UtcNow - _createTime).TotalMilliseconds;
      }
    }

    /// <summary>
    /// Returns that connection is registered or not.
    /// </summary>
    public bool IsRegistered
    {
      [SecurityCritical]
      get
      {
        ThrowIfDisposed();
        return Id != null && !Id.Contains(TempConnectionPrefix);
      }
    }
    #endregion

    #region public methods
    /// <summary>
    /// Sends server info to client side. This method starts handshake protocol.
    /// </summary>
    [SecurityCritical]
    public void SendServerInfo()
    {
      var info = new ServerInfo();
      info.ApiName = _serverApiName;
      SendMessage(ServerInfo, info);
    }

    /// <summary>
    /// Registers connection with new id.
    /// </summary>
    /// <param name="newId">New connection identifier.</param>
    [SecurityCritical]
    public void Register(string newId)
    {
      ThrowIfDisposed();
      Id = newId;
    }
    #endregion

    #region override methods
    [SecuritySafeCritical]
    protected override void OnPackagePartReceived()
    {
      _lastActivity = DateTime.UtcNow;
    }

    [SecuritySafeCritical]
    protected override void OnPackageReceived(PackageReceivedEventArgs args)
    {
      if (args.Exception != null)
      {
        var se = args.Exception as SocketException;
        if (se != null && se.SocketErrorCode == SocketError.ConnectionReset)
          return;

        _logger.Write(args.Exception);
        return;
      }

      var temp = Volatile.Read(ref _receivedCallback);
      if (temp != null)
        temp(this, args);
    }

    [SecuritySafeCritical]
    protected override void OnPackageSent(PackageSendedEventArgs args)
    {
      if (args.Exception != null)
        _logger.Write(args.Exception);
      else
        _lastActivity = DateTime.UtcNow;
    }
    #endregion
  }
}
