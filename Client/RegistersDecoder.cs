using System;
using System.Text;

namespace Client
{
    /// <summary>
    /// Декодер для преобразования raw Modbus регистров (ushort массив) в типизированные данные.
    /// 
    /// Modbus хранит все данные в виде 16-битных слов (ushort). Этот класс предоставляет методы
    /// для декодирования этих слов в реальные типы данных:
    /// - INT (целые числа): 1 регистр = 1 значение
    /// - REAL (числа с плавающей точкой): 2 регистра = 1 значение (IEEE 754)
    /// - STRING (текстовые данные): 3+ регистра = текст, кодированный как ASCII/ANSI
    /// - DATE (дата/время): 2 регистра = UNIX timestamp (4 байта)
    /// - DWORD (32-битные целые числа): 2 регистра = 1 значение
    /// 
    /// Структура регистров (используемая в этом проекте):
    /// [0-1]   : INT
    /// [2-3]   : REAL
    /// [4-9]   : STRING (6 символов = 3 регистра)
    /// [10-11] : DATE
    /// [12-13] : DWORD
    /// [14-19] : дополнительные INT значения
    /// </summary>
    internal class RegistersDecoder
    {
        // Ссылка на массив регистров для декодирования
        private readonly ushort[] _registers;

        /// <summary>
        /// Конструктор RegistersDecoder.
        /// </summary>
        /// <param name="registers">Массив ushort значений (Holding Registers) с сервера Modbus</param>
        public RegistersDecoder(ushort[] registers)
        {
            _registers = registers ?? throw new ArgumentNullException(nameof(registers));
        }

        /// <summary>
        /// Декодирует целое число (INT) из одного регистра.
        /// 
        /// INT в Modbus — это 16-битное целое число, помещающееся в один регистр.
        /// Может быть подписанным (signed) или беззнаковым (unsigned).
        /// 
        /// Обычно используются адреса: 0, 1, 14, 15, 16, 17, 18, 19
        /// </summary>
        /// <param name="address">Адрес регистра (0..99)</param>
        /// <returns>Целое число (int) из регистра</returns>
        public int DecodeInt(int address)
        {
            ValidateAddress(address);
            // Преобразуем ushort в int (сохраняя значение)
            return (int)_registers[address];
        }

        /// <summary>
        /// Декодирует число с плавающей точкой (REAL) из двух регистров.
        /// 
        /// REAL в Modbus — это 32-битное число IEEE 754, занимающее 2 регистра (4 байта).
        /// Используется для представления дробных чисел (например, температура 25.5°C).
        /// 
        /// Регистры расположены последовательно: [address, address+1]
        /// Адреса: 2-3
        /// </summary>
        /// <param name="address">Адрес первого регистра из пары (должен быть чётным или нечётным, но последовательным)</param>
        /// <returns>Число с плавающей точкой (float)</returns>
        public float DecodeReal(int address)
        {
            ValidateAddress(address);
            ValidateAddress(address + 1);

            // Объединяем два регистра в 4 байта
            byte[] bytes = new byte[4];
            bytes[0] = (byte)(_registers[address] & 0xFF);
            bytes[1] = (byte)((_registers[address] >> 8) & 0xFF);
            bytes[2] = (byte)(_registers[address + 1] & 0xFF);
            bytes[3] = (byte)((_registers[address + 1] >> 8) & 0xFF);

            // Преобразуем в float (IEEE 754)
            return BitConverter.ToSingle(bytes, 0);
        }

        /// <summary>
        /// Декодирует текстовую строку (STRING) из нескольких регистров.
        /// 
        /// STRING в Modbus — это текст, кодированный в ANSI/ASCII, где каждый регистр
        /// содержит 2 символа (2 байта на 1 регистр).
        /// 
        /// Строка считается завершённой, если встречается нулевой байт (\\0).
        /// Адреса: 4-9 (3 регистра = 6 символов)
        /// </summary>
        /// <param name="address">Адрес первого регистра строки</param>
        /// <param name="length">Количество регистров (обычно 3 для 6 символов)</param>
        /// <returns>Строка текста</returns>
        public string DecodeString(int address, int length)
        {
            ValidateAddress(address);
            ValidateAddress(address + length - 1);

            var sb = new StringBuilder();

            for (int i = 0; i < length; i++)
            {
                ushort register = _registers[address + i];

                // Первый байт (младший)
                byte byte1 = (byte)(register & 0xFF);
                if (byte1 != 0) sb.Append((char)byte1);

                // Второй байт (старший)
                byte byte2 = (byte)((register >> 8) & 0xFF);
                if (byte2 != 0) sb.Append((char)byte2);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Декодирует дату/время (DATE) из двух регистров в формате UNIX timestamp.
        /// 
        /// DATE в Modbus — это 32-битное целое число (UNIX timestamp), 
        /// представляющее количество секунд с 1970-01-01 00:00:00 UTC.
        /// Занимает 2 регистра (4 байта).
        /// 
        /// Адреса: 10-11
        /// </summary>
        /// <param name="address">Адрес первого регистра из пары</param>
        /// <returns>DateTime объект</returns>
        public DateTime DecodeDate(int address)
        {
            ValidateAddress(address);
            ValidateAddress(address + 1);

            // Объединяем два регистра в 32-битное целое число (UNIX timestamp)
            uint timestamp = (uint)((_registers[address + 1] << 16) | _registers[address]);

            // Преобразуем UNIX timestamp в DateTime
            DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            DateTime dateTime = epoch.AddSeconds(timestamp);

            return dateTime;
        }

        /// <summary>
        /// Декодирует 32-битное целое число без знака (DWORD) из двух регистров.
        /// 
        /// DWORD в Modbus — это 32-битное целое число (0..4,294,967,295),
        /// занимающее 2 регистра (4 байта).
        /// Используется для больших значений, которые не помещаются в один INT.
        /// 
        /// Адреса: 12-13
        /// </summary>
        /// <param name="address">Адрес первого регистра из пары</param>
        /// <returns>32-битное целое число (uint)</returns>
        public uint DecodeDword(int address)
        {
            ValidateAddress(address);
            ValidateAddress(address + 1);

            // Объединяем два регистра: первый регистр — младшие 16 бит, второй — старшие 16 бит
            uint dword = (uint)((_registers[address + 1] << 16) | _registers[address]);

            return dword;
        }

        /// <summary>
        /// Деодирует подписанное целое число (SINT — signed int) из одного регистра.
        /// 
        /// SINT — это подписанное 16-битное целое число (-32768..32767).
        /// Хранится как дополнительный код (two's complement).
        /// </summary>
        /// <param name="address">Адрес регистра</param>
        /// <returns>Подписанное целое число (short)</returns>
        public short DecodeSint(int address)
        {
            ValidateAddress(address);
            return (short)_registers[address];
        }

        /// <summary>
        /// Вспомогательный метод для валидации адреса регистра.
        /// Проверяет, что адрес находится в пределах массива регистров.
        /// </summary>
        /// <param name="address">Адрес для проверки</param>
        /// <exception cref="IndexOutOfRangeException">Если адрес выходит за границы массива</exception>
        private void ValidateAddress(int address)
        {
            if (address < 0 || address >= _registers.Length)
            {
                throw new IndexOutOfRangeException(
                    $"Адрес регистра {address} выходит за границы массива (размер: {_registers.Length})");
            }
        }

        /// <summary>
        /// Возвращает информацию о всех декодированных значениях в сыром виде.
        /// Полезно для отладки и проверки корректности декодирования.
        /// </summary>
        /// <returns>Строка с информацией о всех значениях</returns>
        public string GetDebugInfo()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== Registers Debug Info ===");

            try
            {
                sb.AppendLine($"INT[0]: {DecodeInt(0)}");
                sb.AppendLine($"INT[1]: {DecodeInt(1)}");
                sb.AppendLine($"REAL[2-3]: {DecodeReal(2):F2}");
                sb.AppendLine($"STRING[4-6]: {DecodeString(4, 3)}");
                sb.AppendLine($"DATE[10-11]: {DecodeDate(10):yyyy-MM-dd HH:mm:ss UTC}");
                sb.AppendLine($"DWORD[12-13]: {DecodeDword(12)}");

                // Дополнительные INT значения
                for (int i = 14; i < 20 && i < _registers.Length; i++)
                {
                    sb.AppendLine($"INT[{i}]: {DecodeInt(i)}");
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"Ошибка декодирования: {ex.Message}");
            }

            return sb.ToString();
        }
    }
}
