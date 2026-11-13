using System;

namespace SmartArchiver.Optimization
{
    /// <summary>
    /// Представляє хромосому (набір генів) для налаштування параметрів архіватора
    /// Базовий рівень оптимізації включає 3 ключові параметри
    /// </summary>
    public class ArchiverParameters
    {
        /// <summary>
        /// Ген 1: Розмір вікна алгоритму LZ77 (багаторівневий діапазон, наприклад, 4KB - 32KB)
        /// </summary>
        public int WindowSize { get; set; }           
        
        /// <summary>
        /// Ген 2: Мінімальна довжина збігу для LZ77
        /// </summary>
        public int MinMatchLength { get; set; }       
        
        /// <summary>
        /// Ген 3: Поріг відмови від стиснення (у відсотках)
        /// </summary>
        public int NoCompressionThreshold { get; set; }

        /// <summary>
        /// Створює точну незалежну копію поточних параметрів (необхідно для коректної роботи елітизму та кросоверу).
        /// </summary>
        public ArchiverParameters Clone()
        {
            return new ArchiverParameters
            {
                WindowSize = this.WindowSize,
                MinMatchLength = this.MinMatchLength,
                NoCompressionThreshold = this.NoCompressionThreshold
            };
        }

        /// <summary>
        /// Зручне текстове представлення хромосоми для логування
        /// </summary>
        public override string ToString() => 
            $"[Window: {WindowSize}, MinMatch: {MinMatchLength}, Threshold: {NoCompressionThreshold}]";
    }

    /// <summary>
    /// Особина популяції у генетичному алгоритмі
    /// Зберігає хромосому (параметри) та значення її пристосованості (Fitness / Метрика Q)
    /// </summary>
    public class Individual
    {
        public ArchiverParameters Parameters { get; set; }
        
        /// <summary>
        /// Значення пристосованості (Q-метрика). Чим вище, тим краще стиснення/швидкодія
        /// </summary>
        public double Fitness { get; set; }
    }
}