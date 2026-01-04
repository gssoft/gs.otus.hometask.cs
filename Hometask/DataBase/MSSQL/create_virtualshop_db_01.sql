-- ============================================
-- СОЗДАНИЕ БАЗЫ ДАННЫХ ВИРТУАЛЬНОГО МАГАЗИНА
-- ============================================

-- Создаем базу данных с поддержкой кириллицы
CREATE DATABASE VirtualShop
COLLATE Cyrillic_General_CI_AS;
GO

-- Используем созданную базу данных
USE VirtualShop;
GO

-- ============================================
-- СОЗДАНИЕ ТАБЛИЦ
-- ============================================

-- Таблица "Products" (Продукты)
CREATE TABLE Products (
    ProductID INT IDENTITY(1,1) PRIMARY KEY,
    ProductName NVARCHAR(100) NOT NULL,
    Description NVARCHAR(MAX),
    Price DECIMAL(10,2) NOT NULL,
    QuantityInStock INT NOT NULL,
    
    -- Ограничения для проверки данных
    CONSTRAINT CHK_Price_Positive CHECK (Price >= 0),
    CONSTRAINT CHK_Quantity_NonNegative CHECK (QuantityInStock >= 0)
);
GO

-- Таблица "Users" (Пользователи)
CREATE TABLE Users (
    UserID INT IDENTITY(1,1) PRIMARY KEY,
    UserName NVARCHAR(50) NOT NULL,
    Email NVARCHAR(100) NOT NULL,
    RegistrationDate DATETIME DEFAULT GETDATE(),
    
    -- Уникальность email
    CONSTRAINT UQ_Email UNIQUE (Email),
    
    -- Проверка формата email (базовая)
    CONSTRAINT CHK_Email_Format CHECK (Email LIKE '%@%.%')
);
GO

-- Таблица "Orders" (Заказы)
CREATE TABLE Orders (
    OrderID INT IDENTITY(1,1) PRIMARY KEY,
    UserID INT NOT NULL,
    OrderDate DATETIME DEFAULT GETDATE(),
    Status NVARCHAR(20) DEFAULT N'В обработке',
    
    -- Внешний ключ на таблицу Users
    CONSTRAINT FK_Orders_Users FOREIGN KEY (UserID)
        REFERENCES Users(UserID)
        ON DELETE CASCADE
        ON UPDATE CASCADE,
    
    -- Проверка допустимых статусов
    CONSTRAINT CHK_Status CHECK (Status IN (
        N'В обработке', 
        N'Подтвержден', 
        N'Отправлен', 
        N'Доставлен', 
        N'Отменен'
    ))
);
GO

-- Таблица "OrderDetails" (Детали заказа)
CREATE TABLE OrderDetails (
    OrderDetailID INT IDENTITY(1,1) PRIMARY KEY,
    OrderID INT NOT NULL,
    ProductID INT NOT NULL,
    Quantity INT NOT NULL,
    TotalCost DECIMAL(10,2) NOT NULL,
    
    -- Внешние ключи
    CONSTRAINT FK_OrderDetails_Orders FOREIGN KEY (OrderID)
        REFERENCES Orders(OrderID)
        ON DELETE CASCADE
        ON UPDATE CASCADE,
        
    CONSTRAINT FK_OrderDetails_Products FOREIGN KEY (ProductID)
        REFERENCES Products(ProductID)
        ON DELETE NO ACTION
        ON UPDATE CASCADE,
    
    -- Проверки данных
    CONSTRAINT CHK_Quantity_Positive CHECK (Quantity > 0),
    CONSTRAINT CHK_TotalCost_NonNegative CHECK (TotalCost >= 0)
);
GO

-- ============================================
-- СОЗДАНИЕ ИНДЕКСОВ ДЛЯ УЛУЧШЕНИЯ ПРОИЗВОДИТЕЛЬНОСТИ
-- ============================================

-- Индекс для быстрого поиска продуктов по названию
CREATE INDEX IX_Products_ProductName ON Products(ProductName);

-- Индекс для поиска пользователей по email
CREATE INDEX IX_Users_Email ON Users(Email);

-- Индекс для быстрого поиска заказов по пользователю
CREATE INDEX IX_Orders_UserID ON Orders(UserID);

-- Индекс для поиска заказов по статусу
CREATE INDEX IX_Orders_Status ON Orders(Status);

-- Индекс для быстрого поиска деталей заказа по OrderID
CREATE INDEX IX_OrderDetails_OrderID ON OrderDetails(OrderID);

-- Индекс для быстрого поиска деталей заказа по ProductID
CREATE INDEX IX_OrderDetails_ProductID ON OrderDetails(ProductID);
GO

-- ============================================
-- СОЗДАНИЕ ПРЕДСТАВЛЕНИЙ (VIEWS)
-- ============================================

-- Представление для просмотра информации о заказах с именами пользователей
CREATE VIEW vw_OrdersWithUsers AS
SELECT 
    o.OrderID,
    o.OrderDate,
    o.Status,
    u.UserID,
    u.UserName,
    u.Email
FROM Orders o
INNER JOIN Users u ON o.UserID = u.UserID;
GO

-- Представление для просмотра деталей заказов с информацией о продуктах
CREATE VIEW vw_OrderDetailsWithProducts AS
SELECT 
    od.OrderDetailID,
    od.OrderID,
    p.ProductID,
    p.ProductName,
    od.Quantity,
    p.Price AS UnitPrice,
    od.TotalCost
FROM OrderDetails od
INNER JOIN Products p ON od.ProductID = p.ProductID;
GO

-- Представление для отслеживания остатков товаров
CREATE VIEW vw_ProductStock AS
SELECT 
    ProductID,
    ProductName,
    Price,
    QuantityInStock,
    CASE 
        WHEN QuantityInStock = 0 THEN N'Нет в наличии'
        WHEN QuantityInStock < 5 THEN N'Мало осталось'
        ELSE N'В наличии'
    END AS StockStatus
FROM Products;
GO

PRINT N'============================================';
PRINT N'БАЗА ДАННЫХ VIRTUАLSHOP УСПЕШНО СОЗДАНА!';
PRINT N'Таблицы: Products, Users, Orders, OrderDetails';
PRINT N'Созданы индексы и представления';
PRINT N'============================================';
GO