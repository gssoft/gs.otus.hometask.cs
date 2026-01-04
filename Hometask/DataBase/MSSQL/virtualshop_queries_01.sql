-- ============================================
-- SQL ЗАПРОСЫ ДЛЯ ВИРТУАЛЬНОГО МАГАЗИНА
-- ============================================

-- Используем базу данных VirtualShop
USE VirtualShop;
GO

-- ============================================
-- ТЕСТОВЫЕ ДАННЫЕ (для проверки запросов)
-- ============================================

PRINT N'ДОБАВЛЕНИЕ ТЕСТОВЫХ ДАННЫХ...';
GO

-- Добавляем пользователей (используем N для русских строк)
INSERT INTO Users (UserName, Email) VALUES
(N'Иван Иванов', N'ivan@mail.com'),
(N'Мария Петрова', N'maria@mail.com'),
(N'Алексей Сидоров', N'alex@mail.com'),
(N'Елена Кузнецова', N'elena@mail.com'),
(N'Дмитрий Смирнов', N'dmitry@mail.com');
GO

-- Добавляем продукты
INSERT INTO Products (ProductName, Description, Price, QuantityInStock) VALUES
(N'Ноутбук ASUS', N'15.6 дюймов, Intel Core i5, 8GB RAM', 45999.99, 8),
(N'Смартфон Xiaomi', N'6.5 дюймов, 128GB, Android 13', 24999.50, 15),
(N'Наушники JBL', N'Беспроводные, с шумоподавлением', 7999.99, 25),
(N'Мышь Logitech', N'Проводная, оптическая, 3 кнопки', 1499.99, 4),
(N'Клавиатура Defender', N'Мембранная, USB, подсветка', 2999.99, 12),
(N'Монитор Samsung', N'24 дюйма, Full HD, IPS матрица', 18999.99, 6),
(N'Планшет Huawei', N'10.4 дюйма, 64GB, стилус в комплекте', 32999.99, 3),
(N'Внешний SSD', N'500GB, USB 3.2, скорость до 550MB/s', 6499.99, 10),
(N'Веб-камера', N'Full HD 1080p, автофокус, микрофон', 4499.99, 2),
(N'Фитнес-браслет', N'Отслеживание пульса, сна, уведомления', 3499.99, 18);
GO

-- Добавляем заказы
INSERT INTO Orders (UserID, Status) VALUES
(1, N'Доставлен'),
(2, N'Подтвержден'),
(3, N'В обработке'),
(1, N'Отправлен'),
(4, N'Доставлен'),
(5, N'Подтвержден');
GO

-- Добавляем детали заказов
INSERT INTO OrderDetails (OrderID, ProductID, Quantity, TotalCost) VALUES
(1, 1, 1, 45999.99),  -- Ноутбук
(1, 3, 1, 7999.99),   -- Наушники
(2, 2, 2, 49999.00),  -- Два смартфона
(3, 5, 1, 2999.99),   -- Клавиатура
(4, 7, 1, 32999.99),  -- Планшет
(4, 9, 1, 4499.99),   -- Веб-камера
(5, 4, 3, 4499.97),   -- Три мыши
(6, 10, 2, 6999.98);  -- Два фитнес-браслета
GO

PRINT N'ТЕСТОВЫЕ ДАННЫЕ УСПЕШНО ДОБАВЛЕНЫ!';
PRINT N'';
GO

-- ============================================
-- ОСНОВНЫЕ ЗАПРОСЫ ПО ЗАДАНИЮ
-- ============================================

PRINT N'=== 1. ДОБАВЛЕНИЕ НОВОГО ПРОДУКТА ===';
-- Добавление нового продукта в каталог
INSERT INTO Products (ProductName, Description, Price, QuantityInStock)
VALUES (
    N'Игровая консоль PlayStation 5', 
    N'4K игровая консоль, 825GB SSD, поддержка Ray Tracing', 
    64999.99, 
    5
);

-- Проверка добавленного продукта
SELECT * 
FROM Products 
WHERE ProductName = N'Игровая консоль PlayStation 5';
GO

PRINT N'=== 2. ОБНОВЛЕНИЕ ЦЕНЫ ПРОДУКТА ===';
-- Обновление цены продукта с ID = 3 (Наушники JBL)
UPDATE Products 
SET Price = 7499.99 
WHERE ProductID = 3;

-- Проверка обновленной цены
SELECT ProductID, ProductName, Price 
FROM Products 
WHERE ProductID = 3;
GO

PRINT N'=== 3. ВЫБОР ВСЕХ ЗАКАЗОВ ОПРЕДЕЛЕННОГО ПОЛЬЗОВАТЕЛЯ ===';
-- Выбор всех заказов пользователя с ID = 1
SELECT 
    o.OrderID,
    o.OrderDate,
    o.Status,
    u.UserName,
    u.Email
FROM Orders o
INNER JOIN Users u ON o.UserID = u.UserID
WHERE o.UserID = 1
ORDER BY o.OrderDate DESC;
GO

PRINT N'=== 4. РАСЧЕТ ОБЩЕЙ СТОИМОСТИ ЗАКАЗА ===';
-- Расчет общей стоимости для заказа с ID = 1
SELECT 
    o.OrderID,
    u.UserName,
    o.OrderDate,
    SUM(od.TotalCost) AS TotalOrderCost,
    COUNT(od.OrderDetailID) AS ItemsCount
FROM Orders o
INNER JOIN Users u ON o.UserID = u.UserID
INNER JOIN OrderDetails od ON o.OrderID = od.OrderID
WHERE o.OrderID = 1
GROUP BY o.OrderID, u.UserName, o.OrderDate;
GO

PRINT N'=== 5. ПОДСЧЕТ КОЛИЧЕСТВА ТОВАРОВ НА СКЛАДЕ ===';
-- Общее количество товаров на складе
SELECT 
    COUNT(*) AS TotalProducts,
    SUM(QuantityInStock) AS TotalItemsInStock,
    SUM(QuantityInStock * Price) AS TotalStockValue
FROM Products;
GO

PRINT N'=== 6. ПОЛУЧЕНИЕ 5 САМЫХ ДОРОГИХ ТОВАРОВ ===';
-- 5 самых дорогих товаров в магазине
SELECT TOP 5
    ProductID,
    ProductName,
    Price,
    QuantityInStock,
    (Price * QuantityInStock) AS StockValue
FROM Products
ORDER BY Price DESC;
GO

PRINT N'=== 7. СПИСОК ТОВАРОВ С НИЗКИМ ЗАПАСОМ (МЕНЕЕ 5 ШТУК) ===';
-- Товары, которых осталось меньше 5 штук
SELECT 
    ProductID,
    ProductName,
    QuantityInStock,
    Price,
    CASE 
        WHEN QuantityInStock = 0 THEN N'❌ НЕТ В НАЛИЧИИ'
        WHEN QuantityInStock < 3 THEN N'⚠️ ОЧЕНЬ МАЛО'
        ELSE N'⚠️ МАЛО'
    END AS Warning
FROM Products
WHERE QuantityInStock < 5
ORDER BY QuantityInStock ASC;
GO

-- ============================================
-- ДОПОЛНИТЕЛЬНЫЕ ПОЛЕЗНЫЕ ЗАПРОСЫ
-- ============================================

PRINT N'';
PRINT N'=== ДОПОЛНИТЕЛЬНО: ИНФОРМАЦИЯ О ВСЕХ ЗАКАЗАХ ===';
-- Детальная информация о всех заказах
SELECT 
    o.OrderID,
    o.OrderDate,
    o.Status,
    u.UserName,
    u.Email,
    COUNT(od.OrderDetailID) AS ItemsInOrder,
    SUM(od.TotalCost) AS OrderTotal
FROM Orders o
INNER JOIN Users u ON o.UserID = u.UserID
INNER JOIN OrderDetails od ON o.OrderID = od.OrderID
GROUP BY o.OrderID, o.OrderDate, o.Status, u.UserName, u.Email
ORDER BY o.OrderDate DESC;
GO

PRINT N'=== ДОПОЛНИТЕЛЬНО: САМЫЕ ПОПУЛЯРНЫЕ ТОВАРЫ ===';
-- Самые популярные товары (по количеству заказов)
SELECT TOP 5
    p.ProductID,
    p.ProductName,
    SUM(od.Quantity) AS TotalSold,
    COUNT(DISTINCT od.OrderID) AS OrdersCount
FROM Products p
LEFT JOIN OrderDetails od ON p.ProductID = od.ProductID
GROUP BY p.ProductID, p.ProductName
ORDER BY TotalSold DESC;
GO

PRINT N'=== ДОПОЛНИТЕЛЬНО: АКТИВНЫЕ ПОЛЬЗОВАТЕЛИ ===';
-- Пользователи с наибольшим количеством заказов
SELECT 
    u.UserID,
    u.UserName,
    u.Email,
    u.RegistrationDate,
    COUNT(o.OrderID) AS TotalOrders
FROM Users u
LEFT JOIN Orders o ON u.UserID = o.UserID
GROUP BY u.UserID, u.UserName, u.Email, u.RegistrationDate
ORDER BY TotalOrders DESC;
GO

PRINT N'=== ДОПОЛНИТЕЛЬНО: ИСПОЛЬЗОВАНИЕ ПРЕДСТАВЛЕНИЙ ===';
-- Пример использования созданных представлений
PRINT N'-- Заказы с информацией о пользователях:';
SELECT * FROM vw_OrdersWithUsers WHERE Status = N'Доставлен';

PRINT N'-- Детали заказов с информацией о продуктах:';
SELECT * FROM vw_OrderDetailsWithProducts WHERE OrderID = 1;

PRINT N'-- Остатки товаров на складе:';
SELECT * FROM vw_ProductStock WHERE StockStatus != N'В наличии';
GO

PRINT N'=== ДОПОЛНИТЕЛЬНО: ОБЩАЯ СТАТИСТИКА МАГАЗИНА ===';
-- Общая статистика магазина
SELECT 
    -- Пользователи
    (SELECT COUNT(*) FROM Users) AS TotalUsers,
    (SELECT COUNT(*) FROM Users WHERE RegistrationDate > DATEADD(month, -1, GETDATE())) AS NewUsersLastMonth,
    
    -- Товары
    (SELECT COUNT(*) FROM Products) AS TotalProducts,
    (SELECT COUNT(*) FROM Products WHERE QuantityInStock = 0) AS OutOfStockProducts,
    
    -- Заказы
    (SELECT COUNT(*) FROM Orders) AS TotalOrders,
    (SELECT COUNT(*) FROM Orders WHERE Status = N'Доставлен') AS DeliveredOrders,
    (SELECT SUM(TotalCost) FROM OrderDetails) AS TotalRevenue;
GO

-- ============================================
-- ПРАКТИЧЕСКИЕ ЗАДАНИЯ ДЛЯ САМОСТОЯТЕЛЬНОЙ РАБОТЫ
-- ============================================

PRINT N'';
PRINT N'====================================================================';
PRINT N'ПРАКТИЧЕСКИЕ ЗАДАНИЯ ДЛЯ САМОСТОЯТЕЛЬНОЙ РАБОТЫ:';
PRINT N'====================================================================';
PRINT N'1. Добавьте нового пользователя с именем "Сергей Волков" и email "sergey@mail.com"';
PRINT N'2. Обновите статус заказа с ID=3 на "Подтвержден"';
PRINT N'3. Найдите все заказы, которые были сделаны в этом месяце';
PRINT N'4. Рассчитайте среднюю стоимость заказа';
PRINT N'5. Найдите товары, цена которых выше средней цены всех товаров';
PRINT N'6. Получите список пользователей, которые еще не сделали ни одного заказа';
PRINT N'====================================================================';
GO

-- ============================================
-- РЕШЕНИЯ ПРАКТИЧЕСКИХ ЗАДАНИЙ (для проверки)
-- ============================================

PRINT N'';
PRINT N'=== РЕШЕНИЯ: ===';
GO

-- 1. Добавление нового пользователя
PRINT N'1. Добавление нового пользователя:';
INSERT INTO Users (UserName, Email) 
VALUES (N'Сергей Волков', N'sergey@mail.com');
SELECT * FROM Users WHERE Email = N'sergey@mail.com';
GO

-- 2. Обновление статуса заказа
PRINT N'2. Обновление статуса заказа:';
UPDATE Orders SET Status = N'Подтвержден' WHERE OrderID = 3;
SELECT OrderID, Status FROM Orders WHERE OrderID = 3;
GO

-- 3. Заказы этого месяца
PRINT N'3. Заказы этого месяца:';
SELECT * FROM Orders 
WHERE MONTH(OrderDate) = MONTH(GETDATE()) 
  AND YEAR(OrderDate) = YEAR(GETDATE());
GO

-- 4. Средняя стоимость заказа
PRINT N'4. Средняя стоимость заказа:';
SELECT 
    AVG(OrderTotal) AS AverageOrderValue
FROM (
    SELECT 
        o.OrderID,
        SUM(od.TotalCost) AS OrderTotal
    FROM Orders o
    INNER JOIN OrderDetails od ON o.OrderID = od.OrderID
    GROUP BY o.OrderID
) AS OrderTotals;
GO

-- 5. Товары с ценой выше средней
PRINT N'5. Товары с ценой выше средней:';
SELECT 
    ProductID,
    ProductName,
    Price
FROM Products
WHERE Price > (SELECT AVG(Price) FROM Products)
ORDER BY Price DESC;
GO

-- 6. Пользователи без заказов
PRINT N'6. Пользователи без заказов:';
SELECT 
    u.UserID,
    u.UserName,
    u.Email,
    u.RegistrationDate
FROM Users u
LEFT JOIN Orders o ON u.UserID = o.UserID
WHERE o.OrderID IS NULL;
GO

PRINT N'';
PRINT N'============================================';
PRINT N'ВСЕ ЗАПРОСЫ УСПЕШНО ВЫПОЛНЕНЫ!';
PRINT N'============================================';
GO