using System;
using System.Text;
using ModbusRx.Device;

namespace Server
{
    /// <summary>
    /// Менеджер данных Modbus сервера.
    /// 
    /// Этот класс отвечает за управление симуляцией данных (Coils и Holding Registers)
    /// в памяти Modbus сервера. Включает методы для инициализации данных и их периодического
    /// обновления по определённым алгоритмам.
    /// 
    /// Основные компоненты:
    /// - Циклическое включение/выключение Coils (0..9)
    /// - Генерирование разнообразных типов данных в Holding Registers (INT, REAL, STRING, DATE, DWORD)
    /// </summary>
    internal class ModbusServerDataManager
    {
        // Ссылка на сервер Modbus для доступа к DataStore
        private readonly ModbusServer _server;

        // Счётчик для отслеживания позиции в цикле включения/выключения Coils
        private int _coilCycleIndex = 0;

        // Счётчик для внутреннего состояния цикла (0 = фаза включения, 1 = фаза выключения)
        private int _cyclePhase = 0;

        // Random для генерирования случайных значений в регистрах
        private readonly Random _random = new Random();

        /// <summary>
        /// Конструктор ModbusServerDataManager.
        /// </summary>
        /// <param name="server">Экземпляр ModbusServer для управления</param>
        public ModbusServerDataManager(ModbusServer server)
        {
            _server = server ?? throw new ArgumentNullException(nameof(server));
        }

        /// <summary>
        /// Инициализирует данные сервера: создаёт массивы Coils и Holding Registers.
        /// 
        /// Структура регистров:
        /// - [0-1]   : INT (целое число, 1 регистр каждое слово)
        /// - [2-3]   : REAL (число с плавающей точкой, 2 регистра = 4 байта)
        /// - [4-9]   : STRING (текстовые данные, 6 символов = 3 регистра)
        /// - [10-11] : DATE (UNIX timestamp, 2 регистра для 4-байтного значения)
        /// - [12-13] : DWORD (4-байтное целое число, 2 регистра)
        /// - [14-19] : дополнительные INT значения для демонстрации
        /// </summary>
        public void InitializeData()
        {
            // Инициализируем 100 Coils (булевых) — на самом деле используем 0..9
            var coils = new bool[100];

            // Инициализируем 100 Holding Registers (ushort) — используем 0..19
            var registers = new ushort[100];

            _server.LoadSimulationData(coils: coils, holdingRegisters: registers);

            Console.WriteLine("[Server] Данные инициализированы: 100 Coils, 100 Holding Registers.");
        }

        /// <summary>
        /// Симулирует циклическое включение и выключение Coils[0..9].
        /// 
        /// Алгоритм работы:
        /// 1. Сначала включаются все Coils последовательно: 0→1→2→...→9 (всё TRUE)
        /// 2. Затем выключаются последовательно: 9→8→7→...→0 (всё FALSE)
        /// 3. Затем цикл повторяется
        /// 
        /// ВАЖНО: DataStore в ModbusRx использует 1-based индексирование (индекс 0 зарезервирован).
        /// Для адреса Modbus 0, нужно использовать DataStore[1], для адреса 1 -> DataStore[2] и т.д.
        /// 
        /// Метод должен вызываться периодически (например, каждую итерацию цикла симуляции).
        /// </summary>
        public void SimulateCoilsCycle()
        {
            // Если находимся в фазе включения (фаза 0)
            if (_cyclePhase == 0)
            {
                // Включаем Coil с индексом _coilCycleIndex
                // Используем +1 для 1-based индексирования DataStore
                _server.DataStore.CoilDiscretes[_coilCycleIndex + 1] = true;

                _coilCycleIndex++;

                // Если прошли все 10 Coils (0..9), переходим на фазу выключения
                if (_coilCycleIndex >= 10)
                {
                    _coilCycleIndex = 9; // Начинаем выключение с конца (индекс 9)
                    _cyclePhase = 1;     // Переходим в фазу выключения
                }
            }
            // Если находимся в фазе выключения (фаза 1)
            else if (_cyclePhase == 1)
            {
                // Выключаем Coil с индексом _coilCycleIndex
                // Используем +1 для 1-based индексирования DataStore
                _server.DataStore.CoilDiscretes[_coilCycleIndex + 1] = false;

                _coilCycleIndex--;

                // Если выключили все (вышли за индекс 0), переходим обратно к фазе включения
                if (_coilCycleIndex < 0)
                {
                    _coilCycleIndex = 0; // Начинаем включение с начала (индекс 0)
                    _cyclePhase = 0;     // Переходим в фазу включения
                }
            }
        }

        /// <summary>
        /// Симулирует обновление Holding Registers с использованием разных типов данных.
        /// 
        /// Регистры хранят:
        /// - INT (адреса 0-1): целое число (16-бит в одном регистре)
        /// - REAL (адреса 2-3): число с плавающей точкой (32-бит = 2 регистра)
        /// - STRING (адреса 4-9): текстовая строка (3 регистра = 6 символов)
        /// - DATE (адреса 10-11): дата/время в формате UNIX timestamp (4 байта = 2 регистра)
        /// - DWORD (адреса 12-13): 32-битное целое число (4 байта = 2 регистра)
        /// - Дополнительные INT (адреса 14-19): для демонстрации
        /// 
        /// Метод должен вызываться периодически для обновления значений.
        /// </summary>
        public void SimulateRegistersWithTypes()
        {
            // ВАЖНО: DataStore в ModbusRx использует 1-based индексирование (индекс 0 зарезервирован).
            // Для адреса Modbus 0, нужно использовать DataStore[1], для адреса 1 -> DataStore[2] и т.д.

            // ===== INT: регистры 0-1 (один INT на один регистр) =====
            // Адреса Modbus 0-1 -> DataStore[1-2]
            // Генерируем случайное целое число в диапазоне 0..1000
            ushort intValue = (ushort)_random.Next(0, 1001);
            _server.DataStore.HoldingRegisters[1] = intValue;
            _server.DataStore.HoldingRegisters[2] = (ushort)_random.Next(0, 1001);

            // ===== REAL: регистры 2-3 (32-бит floating point = 2 регистра) =====
            // Адреса Modbus 2-3 -> DataStore[3-4]
            // Генерируем случайное число с плавающей точкой
            float realValue = (float)_random.NextDouble() * 100f; // 0.0 .. 100.0

            // Преобразуем float в два ushort регистра (big-endian)
            byte[] realBytes = BitConverter.GetBytes(realValue);
            _server.DataStore.HoldingRegisters[3] = BitConverter.ToUInt16(realBytes, 0);
            _server.DataStore.HoldingRegisters[4] = BitConverter.ToUInt16(realBytes, 2);

            // ===== STRING: регистры 4-9 (6 символов = 3 регистра) =====
            // Адреса Modbus 4-6 -> DataStore[5-7]
            // Генерируем случайную строку из букв и цифр
            string randomString = GenerateRandomString(6);
            byte[] stringBytes = Encoding.ASCII.GetBytes(randomString);

            // Заполняем регистры (каждый регистр = 2 байта = 2 символа)
            for (int i = 0; i < 3 && i * 2 < stringBytes.Length; i++)
            {
                byte high = stringBytes[i * 2];
                byte low = (i * 2 + 1 < stringBytes.Length) ? stringBytes[i * 2 + 1] : (byte)0;
                _server.DataStore.HoldingRegisters[5 + i] = (ushort)((high << 8) | low);
            }

            // ===== DATE: регистры 10-11 (UNIX timestamp = 4 байта = 2 регистра) =====
            // Адреса Modbus 10-11 -> DataStore[11-12]
            // Генерируем случайное время за последний месяц
            DateTime randomDate = DateTime.Now.AddDays(-_random.Next(0, 30));
            long unixTimestamp = (long)(randomDate - new DateTime(1970, 1, 1)).TotalSeconds;

            // Преобразуем 64-бит timestamp в два 32-бит значения (используем только нижние 32 бита)
            uint dateValue = (uint)(unixTimestamp & 0xFFFFFFFF);
            _server.DataStore.HoldingRegisters[11] = (ushort)(dateValue & 0xFFFF);
            _server.DataStore.HoldingRegisters[12] = (ushort)((dateValue >> 16) & 0xFFFF);

            // ===== DWORD: регистры 12-13 (32-битное целое число = 2 регистра) =====
            // Адреса Modbus 12-13 -> DataStore[13-14]
            // Генерируем случайное 32-битное число
            uint dwordValue = (uint)_random.Next();
            _server.DataStore.HoldingRegisters[13] = (ushort)(dwordValue & 0xFFFF);
            _server.DataStore.HoldingRegisters[14] = (ushort)((dwordValue >> 16) & 0xFFFF);

            // ===== Дополнительные INT: регистры 14-19 =====
            // Адреса Modbus 14-19 -> DataStore[15-20]
            // Просто генерируем случайные целые числа
            for (int i = 0; i < 6; i++)
            {
                _server.DataStore.HoldingRegisters[15 + i] = (ushort)_random.Next(0, 10000);
            }
        }

        /// <summary>
        /// Генерирует случайную строку из букв и цифр.
        /// </summary>
        /// <param name="length">Длина генерируемой строки</param>
        /// <returns>Случайная строка</returns>
        private string GenerateRandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var result = new StringBuilder();

            for (int i = 0; i < length; i++)
            {
                result.Append(chars[_random.Next(chars.Length)]);
            }

            return result.ToString();
        }

        /// <summary>
        /// Возвращает текущее состояние Coils для отладки.
        /// </summary>
        /// <returns>Строка с текущим состоянием цикла Coils</returns>
        public string GetCoilsDebugInfo()
        {
            return $"Coil Cycle: Index={_coilCycleIndex}, Phase={(_cyclePhase == 0 ? "ON" : "OFF")}";
        }
    }
}
