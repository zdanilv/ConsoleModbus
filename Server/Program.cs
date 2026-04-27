using ModbusRx.Device;

namespace Server
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            // Создаём сервер
            using var server = new ModbusServer();

            // Включаем SimulationMode для упрощённого заполнения данных
            server.SimulationMode = true;

            // Стартуем Modbus TCP сервер на порту 502, Unit ID = 1
            server.StartTcpServer(port: 502, unitId: 1);

            // Инициализация Coil (булевых) и Holding Registers
            server.LoadSimulationData(
                coils: new bool[100],    // 100 булевых
                holdingRegisters: new ushort[100] // 100 регистров
            );

            Console.WriteLine("Modbus TCP Server запущен на порту 502...");
            Console.WriteLine("Нажми любую клавишу завершить.");

            // Основной цикл записи / чтения
            var random = new Random();
            while (!Console.KeyAvailable)
            {
                // Меняем случайно пару Coil
                for (int i = 1; i < 10; i++)
                {
                    bool value = random.Next(0, 2) == 1;
                    server.DataStore.CoilDiscretes[i] = value;
                }

                // Инкрементируем значения регистра
                for (int i = 1; i < 10; i++)
                {
                    server.DataStore.HoldingRegisters[i] += 1;
                }

                // Отладочный вывод в консоль
                Console.WriteLine($"Server: Coils[0..9] = {string.Join(",", server.DataStore.CoilDiscretes[10])}");
                Console.WriteLine($"Server: HoldingRegs[0..9] = {string.Join(",", server.DataStore.HoldingRegisters[10])}");

                Thread.Sleep(1000); // задержка 1 сек
            }

            server.Stop(); // Остановить сервер
        }
    }
}
