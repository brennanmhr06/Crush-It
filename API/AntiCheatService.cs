using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using CrushIt.Data;

namespace CrushIt.API
{
    public class AntiCheatService
    {
        private readonly IApiClient _apiClient;
        private readonly string _userId;
        private readonly string _sessionId;

        private DateTime _sessionStartTime;
        private DateTime _levelStartTime;
        private int _levelMoves;
        private int _levelMatches;
        private int _currentCombo;
        private int _maxCombo;
        private List<double> _moveTimes = new List<double>();
        private DateTime _lastMoveTime;

        public AntiCheatService(IApiClient apiClient, string userId, string sessionId)
        {
            _apiClient = apiClient;
            _userId = userId;
            _sessionId = sessionId;
            _sessionStartTime = DateTime.UtcNow;
        }

        public async Task<bool> InitializeSessionAsync()
        {

            bool isValid = await _apiClient.ValidateSessionAsync(_userId, _sessionId, DateTime.UtcNow);

            if (!isValid)
            {

                Console.WriteLine($"Session validation failed for user {_userId}");
            }

            return isValid;
        }

        public void StartLevelTracking(int level)
        {
            _levelStartTime = DateTime.UtcNow;
            _levelMoves = 0;
            _levelMatches = 0;
            _currentCombo = 0;
            _maxCombo = 0;
            _moveTimes.Clear();
            _lastMoveTime = DateTime.UtcNow;
        }

        public void RecordMove()
        {
            _levelMoves++;
            var now = DateTime.UtcNow;
            var moveTime = (now - _lastMoveTime).TotalMilliseconds;
            _moveTimes.Add(moveTime);
            _lastMoveTime = now;
        }

        public void RecordMatch(int combo)
        {
            _levelMatches++;
            _currentCombo = combo;
            if (combo > _maxCombo)
                _maxCombo = combo;
        }

        public async Task<bool> ValidateScoreAsync(int level, int score, int moves, TimeSpan playTime)
        {

            if (!IsScoreMathematicallyPossible(score, moves, playTime))
            {
                Console.WriteLine($"Score validation failed: Score {score} not mathematically possible for {moves} moves in {playTime}");
                return false;
            }


            bool serverValid = await _apiClient.ValidateScoreAsync(_userId, level, score, moves, playTime);

            if (!serverValid)
            {
                Console.WriteLine($"Server rejected score {score} for level {level}");
            }

            return serverValid;
        }

        public async Task<bool> VerifyAchievementAsync(AchievementType achievementType, object proofData)
        {

            bool isValid = await _apiClient.VerifyAchievementAsync(_userId, achievementType, proofData);

            if (!isValid)
            {
                Console.WriteLine($"Achievement {achievementType} verification failed for user {_userId}");
            }

            return isValid;
        }

        public async Task ReportGameplayPatternAsync(int level)
        {
            var pattern = new GameplayPattern
            {
                UserId = _userId,
                Level = level,
                StartTime = _levelStartTime,
                EndTime = DateTime.UtcNow,
                TotalMoves = _levelMoves,
                TotalMatches = _levelMatches,
                AverageMoveTime = CalculateAverageMoveTime(),
                MaxCombo = _maxCombo,
                RapidMovesCount = CountRapidMoves(),
                ImpossibleMovesCount = CountImpossibleMoves()
            };

            await _apiClient.ReportGameplayPatternAsync(_userId, pattern);
        }

        private bool IsScoreMathematicallyPossible(int score, int moves, TimeSpan playTime)
        {





            if (moves < 1) return false;


            int maxScorePerMatch = 50;
            int maxMatchesPossible = moves / 3;
            int theoreticalMaxScore = maxMatchesPossible * maxScorePerMatch;


            if (score > theoreticalMaxScore * 1.5)
            {
                Console.WriteLine($"Score {score} exceeds theoretical max {theoreticalMaxScore} for {moves} moves");
                return false;
            }


            double secondsPerMove = playTime.TotalSeconds / moves;
            if (secondsPerMove < 0.5)
            {
                Console.WriteLine($"Playtime {playTime} too fast for {moves} moves ({secondsPerMove:F2}s per move)");
                return false;
            }

            if (secondsPerMove > 30)
            {
                Console.WriteLine($"Playtime {playTime} too slow for {moves} moves ({secondsPerMove:F2}s per move)");
                return false;
            }

            return true;
        }

        private double CalculateAverageMoveTime()
        {
            if (_moveTimes.Count == 0) return 0;
            double sum = 0;
            foreach (var time in _moveTimes)
                sum += time;
            return sum / _moveTimes.Count;
        }

        private int CountRapidMoves()
        {

            int rapidCount = 0;
            foreach (var time in _moveTimes)
            {
                if (time < 100)
                    rapidCount++;
            }
            return rapidCount;
        }

        private int CountImpossibleMoves()
        {



            int impossibleCount = 0;


            if (_moveTimes.Count > 10)
            {
                int superFastCount = 0;
                foreach (var time in _moveTimes)
                {
                    if (time < 50)
                        superFastCount++;
                }


                if (superFastCount > _moveTimes.Count * 0.3)
                {
                    impossibleCount = superFastCount;
                }
            }

            return impossibleCount;
        }
    }
}