using System;
using System.Text;

namespace Client
{
    /// <summary>
    /// Утилита для красивого отображения таблицы Modbus данных в консоли.
    /// 
    /// Этот класс отвечает за форматирование и отображение:
    /// - Coils (булевы значения): bCoil_0..bCoil_9
    /// - Holding Registers (типизированные данные): INT, REAL, STRING, DATE, DWORD
    /// 
    /// Обновляет фиксированную область экрана без засорения консоли.
    /// Использует синхронизацию потоков (lock) для безопасности в многопоточной среде.
    /// </summary>
    internal class ModbusDataDisplay
    {
        private readonly int _top;
        private readonly int _left;
        private readonly object _sync = new object();

        public ModbusDataDisplay()
        {
            // Запоминаем текущую позицию курсора — будем перезаписывать экран оттуда
            _top = Console.CursorTop + 1;
            _left = Console.CursorLeft;
        }

        /// <summary>
        /// Обновляет область отображения таблицы с булевыми Coils и числовыми Registers.
        /// Используется стандартное представление (без декодирования типов).
        /// </summary>
        /// <param name="coils">Массив булевых значений Coils[0..9]</param>
        /// <param name="registers">Массив ushort значений Registers[0..19]</param>
        public void Update(bool[] coils, ushort[] registers)
        {
            if (coils == null) throw new ArgumentNullException(nameof(coils));
            if (registers == null) throw new ArgumentNullException(nameof(registers));

            lock (_sync)
            {
                // Перейти к начальной позиции и перезаписать блок
                Console.SetCursorPosition(_left, _top);

                var sb = new StringBuilder();
                sb.AppendLine("----- Modbus Variable Map -----");

                // Выводим Coils
                sb.AppendLine("Coils:");
                for (int i = 0; i < coils.Length; i++)
                {
                    sb.AppendFormat(" bCoil_{0:00}: {1}", i, coils[i] ? "1" : "0");
                    if (i % 5 == 4) sb.AppendLine();
                }

                sb.AppendLine();

                // Выводим Registers в сыром виде
                sb.AppendLine("Holding Registers (raw):");
                for (int i = 0; i < Math.Min(registers.Length, 20); i++)
                {
                    sb.AppendFormat(" iReg_{0:00}: {1,5}", i, registers[i]);
                    if (i % 3 == 2) sb.AppendLine();
                }

                // Очищаем несколько строк под блок (на случай, если предыдущий блок был длиннее)
                int lines = 16;
                for (int i = 0; i < lines; i++)
                {
                    Console.Write(new string(' ', Console.WindowWidth));
                }

                Console.SetCursorPosition(_left, _top);
                Console.Write(sb.ToString());
            }
        }

        /// <summary>
        /// Обновляет область отображения с типизированными данными из Registers.
        /// Использует RegistersDecoder для преобразования raw регистров в реальные типы.
        /// 
        /// Выводит:
        /// - Coils (булевы): bCoil_0..bCoil_9
        /// - INT: целые числа
        /// - REAL: числа с плавающей точкой
        /// - STRING: текстовые данные
        /// - DATE: дата/время
        /// - DWORD: 32-битные целые числа
        /// </summary>
        /// <param name="coils">Массив булевых значений Coils[0..9]</param>
        /// <param name="registers">Массив ushort значений Registers[0..19]</param>
        public void UpdateWithTypes(bool[] coils, ushort[] registers)
        {
            if (coils == null) throw new ArgumentNullException(nameof(coils));
            if (registers == null) throw new ArgumentNullException(nameof(registers));

            lock (_sync)
            {
                // Перейти к начальной позиции и перезаписать блок
                Console.SetCursorPosition(_left, _top);

                var sb = new StringBuilder();
                sb.AppendLine("========== Modbus Typed Data Map ==========");

                // ===== Выводим Coils =====
                sb.AppendLine("Coils (Булевы переменные):");
                for (int i = 0; i < coils.Length; i++)
                {
                    sb.AppendFormat(" bCoil_{0:00}: {1}", i, coils[i] ? "ON " : "OFF");
                    if ((i + 1) % 3 == 0) sb.AppendLine();
                }
                sb.AppendLine();

                // Создаём декодер для типизированных данных
                var decoder = new RegistersDecoder(registers);

                // ===== Выводим типизированные данные =====
                sb.AppendLine("Holding Registers (Типизированные данные):");

                try
                {
                    // INT [0-1]
                    sb.AppendFormat(" INT[0]:  {0,10}\n", decoder.DecodeInt(0));
                    sb.AppendFormat(" INT[1]:  {0,10}\n", decoder.DecodeInt(1));
                    sb.AppendLine();

                    // REAL [2-3]
                    sb.AppendFormat(" REAL[2-3]:  {0,10:F2}\n", decoder.DecodeReal(2));
                    sb.AppendLine();

                    // STRING [4-6]
                    sb.AppendFormat(" STRING[4-6]:  \"{0}\"\n", decoder.DecodeString(4, 3));
                    sb.AppendLine();

                    // DATE [10-11]
                    DateTime date = decoder.DecodeDate(10);
                    sb.AppendFormat(" DATE[10-11]:  {0:yyyy-MM-dd HH:mm:ss}\n", date);
                    sb.AppendLine();

                    // DWORD [12-13]
                    sb.AppendFormat(" DWORD[12-13]:  {0,10}\n", decoder.DecodeDword(12));
                    sb.AppendLine();

                    // Дополнительные INT значения [14-19]
                    sb.AppendLine(" INT[14-19] (дополнительные):");
                    for (int i = 14; i < 20 && i < registers.Length; i++)
                    {
                        sb.AppendFormat("  INT[{0:00}]: {1,10}\n", i, decoder.DecodeInt(i));
                    }
                }
                catch (Exception ex)
                {
                    sb.AppendLine($" [ERROR] Ошибка декодирования: {ex.Message}");
                }

                // Очищаем область под блок
                int lines = 25;
                for (int i = 0; i < lines; i++)
                {
                    Console.Write(new string(' ', Math.Max(0, Console.WindowWidth - 1)));
                    if (i < lines - 1) Console.WriteLine();
                }

                Console.SetCursorPosition(_left, _top);
                Console.Write(sb.ToString());
            }
        }

        /// <summary>
        /// Пишем строку статуса под областью таблицы.
        /// Используется для вывода сообщений об ошибках, информации подключения и т.д.
        /// </summary>
        /// <param name="text">Текст статуса для вывода</param>
        public void WriteStatus(string text)
        {
            lock (_sync)
            {
                int statusLine = _top + 26;
                if (statusLine >= Console.BufferHeight) statusLine = Console.BufferHeight - 1;

                Console.SetCursorPosition(_left, statusLine);

                // Очищаем старый текст строки статуса
                Console.Write(new string(' ', Math.Max(0, Console.WindowWidth - 1)));

                Console.SetCursorPosition(_left, statusLine);
                Console.WriteLine(text);
            }
        }

        /// <summary>
        /// Выводит сырую отладочную информацию о регистрах.
        /// Полезно для диагностики проблем с подключением или неправильной интерпретацией данных.
        /// </summary>
        /// <param name="registers">Массив регистров для анализа</param>
        public void DisplayRawDebugInfo(ushort[] registers)
        {
            if (registers == null) return;

            lock (_sync)
            {
                var decoder = new RegistersDecoder(registers);
                string debugInfo = decoder.GetDebugInfo();

                Console.SetCursorPosition(_left, _top + 28);
                Console.WriteLine(debugInfo);
            }
        }
    }
}

