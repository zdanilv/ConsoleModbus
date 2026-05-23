using System;
using System.Threading.Tasks;
using CP.IO.Ports;
using ModbusRx.Device;

namespace Client
{
    /// <summary>
    /// Читатель данных Modbus TCP клиента.
    /// 
    /// Этот класс инкапсулирует всю логику асинхронного чтения данных (Coils и Holding Registers)
    /// с Modbus сервера. Обеспечивает чистый интерфейс для работы с данными и обработку ошибок.
    /// 
    /// Основные компоненты:
    /// - Асинхронное чтение Coils (булевых значений)
    /// - Асинхронное чтение Holding Registers (числовых значений)
    /// - Обработка ошибок соединения и тайм-аутов
    /// </summary>
    internal class ModbusClientDataReader
    {
        // Ссылка на Modbus Master для выполнения операций чтения
        private readonly ModbusIpMaster _master;

        // Параметры подключения
        private readonly int _slaveAddress;

        /// <summary>
        /// Конструктор ModbusClientDataReader.
        /// </summary>
        /// <param name="master">Экземпляр ModbusIpMaster для чтения данных</param>
        /// <param name="slaveAddress">Адрес устройства Modbus Slave (обычно 1)</param>
        public ModbusClientDataReader(ModbusIpMaster master, int slaveAddress = 1)
        {
            _master = master ?? throw new ArgumentNullException(nameof(master));
            _slaveAddress = slaveAddress;
        }

        /// <summary>
        /// Асинхронно читает Coils (булевы значения) с сервера Modbus.
        /// 
        /// Параметры чтения:
        /// - Адрес начала: 0
        /// - Количество: 10 (Coils[0..9])
        /// 
        /// В случае ошибки возвращает null. Ошибка логируется через исключение.
        /// </summary>
        /// <returns>Массив булевых значений (true/false) или null при ошибке</returns>
        public async Task<bool[]> ReadCoilsAsync()
        {
            try
            {
                // Читаем 10 Coils с адреса 0
                bool[] coils = await _master.ReadCoilsAsync(
                    slaveAddress: (byte)_slaveAddress,
                    startAddress: 0,
                    numberOfPoints: 10
                );

                return coils;
            }
            catch (Exception ex)
            {
                // При ошибке выбрасываем исключение для обработки на уровне вызывающего кода
                throw new ModbusReadException($"Ошибка при чтении Coils: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Асинхронно читает Holding Registers (числовые значения) с сервера Modbus.
        /// 
        /// Параметры чтения:
        /// - Адрес начала: 0
        /// - Количество: 20 (HoldingRegisters[0..19]) для поддержки всех типов данных
        ///   (INT, REAL, STRING, DATE, DWORD)
        /// 
        /// В случае ошибки возвращает null. Ошибка логируется через исключение.
        /// </summary>
        /// <returns>Массив ushort значений или null при ошибке</returns>
        public async Task<ushort[]> ReadRegistersAsync()
        {
            try
            {
                // Читаем 20 Holding Registers с адреса 0
                // Это достаточно для всех типов данных: INT(2), REAL(2), STRING(3), 
                // DATE(2), DWORD(2), дополнительные INT(7) = 20 регистров
                ushort[] registers = await _master.ReadHoldingRegistersAsync(
                    slaveAddress: (byte)_slaveAddress,
                    startAddress: 0,
                    numberOfPoints: 20
                );

                return registers;
            }
            catch (Exception ex)
            {
                // При ошибке выбрасываем исключение для обработки на уровне вызывающего кода
                throw new ModbusReadException($"Ошибка при чтении Holding Registers: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Выполняет одну полную итерацию чтения: Coils и Registers одновременно.
        /// 
        /// Этот метод удобен для синхронизированного чтения всех данных в один момент времени.
        /// </summary>
        /// <returns>Кортеж (coils, registers) или (null, null) при ошибке</returns>
        public async Task<(bool[] coils, ushort[] registers)> ReadAllDataAsync()
        {
            try
            {
                // Выполняем оба чтения параллельно для оптимизации времени
                var coilsTask = ReadCoilsAsync();
                var registersTask = ReadRegistersAsync();

                // Ожидаем завершения обеих операций
                await Task.WhenAll(coilsTask, registersTask);

                return (coilsTask.Result, registersTask.Result);
            }
            catch (AggregateException ae)
            {
                // При ошибке выбрасываем новое исключение с контекстом
                throw new ModbusReadException(
                    $"Ошибка при чтении данных: {string.Join(", ", ae.InnerExceptions.Select(e => e.Message))}", 
                    ae);
            }
            catch (Exception ex)
            {
                throw new ModbusReadException($"Неожиданная ошибка при чтении данных: {ex.Message}", ex);
            }
        }
    }

    /// <summary>
    /// Специализированное исключение для ошибок чтения Modbus.
    /// Используется для отделения ошибок Modbus от других типов исключений.
    /// </summary>
    public class ModbusReadException : Exception
    {
        public ModbusReadException(string message) : base(message) { }
        public ModbusReadException(string message, Exception innerException) : base(message, innerException) { }
    }
}
