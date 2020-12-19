﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CarpetRadar.Services.Models;
using Cassandra;
using NLog;

namespace CarpetRadar.Services.DataStorage
{
    public interface IDataStorage
    {
        Task<string> AddCurrentCoordinates(Coordinates coordinates, Guid userId);

        Task<IEnumerable<Coordinates>> GetCurrentPositions();

        Task SaveUserInfo(string login, Guid userId, long passwordHash, string company);

        Task<(Guid UserId, long Hash)?> GetUserIdAndPasswordHash(string login);

        Task SetUserToken(Guid userId, string token);

        Task<(Guid?, DateTime?)> FindUser(string token);
    }

    public class DataStorage : IDataStorage
    {
        private readonly ISession session;
        private readonly ILogger logger;

        internal DataStorage(ISession session, ILogger logger)
        {
            this.session = session;
            this.logger = logger;

            /// try prepared statements
            /// try sql injection
        }

        public async Task<string> AddCurrentCoordinates(Coordinates coordinates, Guid userId)
        {
            if (string.IsNullOrEmpty(coordinates.Label)
                || string.IsNullOrEmpty(coordinates.License)
                || coordinates.FlightId == Guid.Empty
                || coordinates.X < 0
                || coordinates.Y < 0)
                return "Empty request parameters";

            var c = $"UPDATE {Constants.ColumnFamily.CarpetFlights} SET " +
                    $"user_id = {userId}, " +
                    $"label = '{coordinates.Label}', " +
                    $"license = '{coordinates.License}', " +
                    $"finished = {coordinates.Finished}, " +
                    $"x = x + [{coordinates.X}], " +
                    $"y = y + [{coordinates.Y}], " +
                    $"time = time + [{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}] " +
                    $"WHERE id = {coordinates.FlightId};";
            var addToCarpets = new SimpleStatement(c);

            var addToPositions = new SimpleStatement(
                $"INSERT INTO {Constants.ColumnFamily.CurrentPositions} (user_id, id, label, x, y, finished) VALUES (?, ?, ?, ?, ?, ?)",
                userId, coordinates.FlightId, coordinates.Label, coordinates.X, coordinates.Y, coordinates.Finished);
            await session.ExecuteAsync(new BatchStatement()
                .Add(addToCarpets)
                .Add(addToPositions));
            return null;
        }

        public async Task<IEnumerable<Coordinates>> GetCurrentPositions()
        {
            /// нужно чистить curPos
            var statement = new SimpleStatement(
                $"SELECT label, x, y FROM {Constants.ColumnFamily.CurrentPositions}");
            statement.SetPageSize(100);
            var rs = session.Execute(statement);
            var coordinates = rs.Select(row =>
                new Coordinates
                {
                    Label = row.GetValue<string>("label"),
                    X = row.GetValue<int>("x"),
                    Y = row.GetValue<int>("y"),
                    Finished = row.GetValue<bool>("finished"),
                });
            return coordinates;
        }

        public async Task SaveUserInfo(string login, Guid userId, long passwordHash, string company)
        {
            var statement = new SimpleStatement(
                $"UPDATE {Constants.ColumnFamily.Users} SET " +
                $"id = {userId}, " +
                $"password_hash = {passwordHash}, " +
                $"company = '{company}' " +
                $"WHERE login = '{login}';");
            session.Execute(statement);
        }

        public async Task<(Guid UserId, long Hash)?> GetUserIdAndPasswordHash(string login)
        {
            var statement = new SimpleStatement(
                $"SELECT id, password_hash FROM {Constants.ColumnFamily.Users} " +
                $"WHERE login = '{login}'");
            var rs = session.Execute(statement);
            var userData = rs.FirstOrDefault();
            if (userData == null)
                return null;
            return (UserId: userData.GetValue<Guid>("id"),
                Hash: userData.GetValue<long>("password_hash"));
        }

        public async Task SetUserToken(Guid userId, string token)
        {
            var statement = new SimpleStatement(
                $"UPDATE {Constants.ColumnFamily.Tokens} SET " +
                $"user_id = {userId}, " +
                $"time = {DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()} " +
                $"WHERE token_ = '{token}';");

            session.Execute(statement);
        }

        public async Task<(Guid?, DateTime?)> FindUser(string token)
        {
            var statement = new SimpleStatement(
                $"SELECT user_id, time FROM {Constants.ColumnFamily.Tokens} " +
                $"WHERE token_ = '{token}';");
            var rs = session.Execute(statement);
            var userTokenData = rs.FirstOrDefault();
            var userId = userTokenData?.GetValue<Guid>("user_id");
            var authTime = userTokenData?.GetValue<DateTimeOffset>("time").UtcDateTime;
            return (userId, authTime);
        }
    }
}
