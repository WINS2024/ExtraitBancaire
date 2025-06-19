-- Script SQL pour la base de données extraitbancaire
-- À exécuter sur votre serveur MariaDB

-- Création de la base de données
CREATE DATABASE IF NOT EXISTS `extraitbancaire`;

-- Utilisation de la base de données
USE `extraitbancaire`;

-- Table pour les extraits mensuels
CREATE TABLE IF NOT EXISTS `extraits_mensuels` (
    `annee` INT NOT NULL,
    `mois` INT NOT NULL,
    `nom_mois` VARCHAR(50) NOT NULL,
    `solde_initial` DECIMAL(18, 2) NOT NULL,
    `solde_final` DECIMAL(18, 2) NOT NULL,
    `total_debit` DECIMAL(18, 2) NOT NULL,
    `total_credit` DECIMAL(18, 2) NOT NULL,
    `mouvement_net` DECIMAL(18, 2) NOT NULL,
    `nombre_operations` INT NOT NULL,
    PRIMARY KEY (`annee`, `mois`)
);

-- Table pour les opérations mensuelles
CREATE TABLE IF NOT EXISTS `operations_mensuelles` (
    `id` INT AUTO_INCREMENT PRIMARY KEY, -- Ajout d'un ID auto-incrémenté pour chaque opération
    `annee_extrait` INT NOT NULL,
    `mois_extrait` INT NOT NULL,
    `date_operation` DATETIME NOT NULL,
    `libelle` VARCHAR(255) NOT NULL,
    `debit` DECIMAL(18, 2) NULL,
    `credit` DECIMAL(18, 2) NULL,
    `est_ajoute_manuellement` BOOLEAN NOT NULL,
    -- Clé étrangère vers extraits_mensuels (optionnel, mais bonne pratique)
    FOREIGN KEY (`annee_extrait`, `mois_extrait`) REFERENCES `extraits_mensuels`(`annee`, `mois`) ON DELETE CASCADE
); 