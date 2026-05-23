using System;
using System.Threading.Tasks;
using ModbusRx.Device;

namespace Server
{
    /// <summary>
    /// Простой Modbus TCP сервер для симуляции ПЛК (Slave).
    /// 
    /// Этот класс служит точкой входа в приложение сервера. Он отвечает за:
    /// - Инициализацию и запуск Modbus TCP сервера на порту 502
    /// - Делегирование управления данными классу ModbusServerDataManager
    /// - Цикл симуляции: периодическое обновление Coils и Registers
    /// 
    /// Основной цикл работы:
    /// 1. Создаётся экземпляр ModbusServer
    /// 2. Сервер запускается в режиме TCP на 127.0.0.1:502
    /// 3. Инициализируются данные (Coils и Registers)
    /// 4. Бесконечный цикл обновляет данные каждую секунду
    /// 5. По нажатию клавиши сервер останавливается
    /// </summary>
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("=== Modbus TCP Server (ПЛК Симулятор) ===");
            Console.WriteLine("Запуск сервера на 127.0.0.1:502 (UnitId=1)...\n");

            // Создаём экземпляр Modbus сервера
            using var server = new ModbusServer();

            // Включаем режим симуляции — сервер будет хранить данные в локальном DataStore
            // (без подключения к реальному оборудованию)
            server.SimulationMode = true;

            // Запускаем TCP сервер на порту 502 с Unit ID = 1
            // Порт 502 — стандартный порт для Modbus TCP (требует прав администратора на Windows)
            // UnitId = 1 — идентификатор устройства (должен совпадать с настройками клиента)
            server.StartTcpServer(port: 502, unitId: 1);

            // Создаём менеджер данных для управления симуляцией
            var dataManager = new ModbusServerDataManager(server);

            // Инициализируем память сервера (100 Coils + 100 Holding Registers)
            dataManager.InitializeData();

            Console.WriteLine("Модbus TCP Server успешно запущен!");
            Console.WriteLine("Нажмите любую клавишу для завершения сервера.\n");

            // Основной цикл симуляции: обновляем данные каждую секунду
            while (!Console.KeyAvailable)
            {
                try
                {
                    // Обновляем состояние Coils: циклическое включение/выключение [0..9]
                    dataManager.SimulateCoilsCycle();

                    // Обновляем Holding Registers с разными типами данных
                    // (INT, REAL, STRING, DATE, DWORD)
                    dataManager.SimulateRegistersWithTypes();

                    // Вывод информации о текущем состоянии (для отладки)
                    DisplayServerStatus(server, dataManager);

                    // Небольшая задержка перед следующей итерацией
                    await Task.Delay(1000);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Ошибка в цикле симуляции: {ex.Message}");
                }
            }

            // Пользователь нажал клавишу — останавливаем сервер
            Console.WriteLine("\nОстановка сервера...");
            server.Stop();
            Console.WriteLine("Сервер остановлен.");
        }

        /// <summary>
        /// Отображает текущее состояние сервера в консоль.
        /// Используется для отладки и мониторинга состояния данных.
        /// </summary>
        /// <param name="server">Экземпляр ModbusServer</param>
        /// <param name="dataManager">Экземпляр ModbusServerDataManager с информацией о состоянии</param>
        private static void DisplayServerStatus(ModbusServer server, ModbusServerDataManager dataManager)
        {
            // Переместиться на позицию для перезаписи (имитация прогресс-бара)
            Console.SetCursorPosition(0, Console.CursorTop);

            // Выводим информацию о текущем состоянии Coils и некоторых Registers
            string coilStatus = $"Coils[0-9]: {GetCoilsStatus(server)}";
            string regStatus = $"Regs[0,2,10]: {server.DataStore.HoldingRegisters[0]}, " +
                             $"{BitConverter.ToSingle(new[] { (byte)(server.DataStore.HoldingRegisters[2] & 0xFF), 
                                                               (byte)((server.DataStore.HoldingRegisters[2] >> 8) & 0xFF), 
                                                               (byte)(server.DataStore.HoldingRegisters[3] & 0xFF), 
                                                               (byte)((server.DataStore.HoldingRegisters[3] >> 8) & 0xFF) }, 0):F2}, " +
                             $"{server.DataStore.HoldingRegisters[10]}";

            Console.WriteLine($"[Server] {coilStatus} | {regStatus} | {dataManager.GetCoilsDebugInfo()}");
        }

        /// <summary>
        /// Возвращает строковое представление состояния Coils[0..9].
        /// TRUE отображается как 1, FALSE как 0.
        /// </summary>
        /// <param name="server">Экземпляр ModbusServer</param>
        /// <returns>Строка вида "1010101010" для состояния Coils</returns>
        private static string GetCoilsStatus(ModbusServer server)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < 10; i++)
            {
                sb.Append(server.DataStore.CoilDiscretes[i] ? "1" : "0");
            }
            return sb.ToString();
        }
    }
}
